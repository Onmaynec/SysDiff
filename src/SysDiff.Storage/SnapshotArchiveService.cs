using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SysDiff.Domain;

namespace SysDiff.Storage;

public sealed class SnapshotArchiveService
{
    private const long MaximumArchiveBytes = 512L * 1024L * 1024L;
    private const long MaximumSnapshotBytes = 1024L * 1024L * 1024L;
    private readonly ISnapshotStore _store;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SnapshotArchiveService(ISnapshotStore store)
    {
        _store = store;
    }

    public async Task<string> ExportAsync(
        SnapshotRecord snapshot,
        string outputPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");

        byte[] snapshotBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        string snapshotHash = Convert.ToHexString(SHA256.HashData(snapshotBytes)).ToLowerInvariant();
        var manifest = new SnapshotArchiveManifest(
            Format: "SysDiff Snapshot",
            FormatVersion: 1,
            SchemaVersion: snapshot.SchemaVersion,
            SysDiffVersion: snapshot.SysDiffVersion,
            SnapshotId: snapshot.Id,
            CreatedAtUtc: DateTimeOffset.UtcNow);
        byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        string manifestHash = Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant();
        byte[] checksumBytes = Encoding.ASCII.GetBytes(
            $"{manifestHash}  manifest.json\n{snapshotHash}  snapshot.json\n");

        string temporary = fullPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using FileStream file = new(
                temporary,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true);
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
            {
                await WriteEntryAsync(archive, "manifest.json", manifestBytes, cancellationToken);
                await WriteEntryAsync(archive, "snapshot.json", snapshotBytes, cancellationToken);
                await WriteEntryAsync(archive, "checksums.sha256", checksumBytes, cancellationToken);
            }

            await file.FlushAsync(cancellationToken);
            File.Move(temporary, fullPath, overwrite: true);
            return fullPath;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public async Task<SnapshotRecord> ImportAsync(
        string archivePath,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(archivePath);
        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Файл снимка не найден.", fullPath);
        }

        if (info.Length > MaximumArchiveBytes)
        {
            throw new InvalidDataException("Архив превышает допустимый размер 512 МБ.");
        }

        await using FileStream file = new(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false);

        ZipArchiveEntry manifestEntry = GetRequiredEntry(archive, "manifest.json");
        ZipArchiveEntry snapshotEntry = GetRequiredEntry(archive, "snapshot.json");
        ZipArchiveEntry checksumsEntry = GetRequiredEntry(archive, "checksums.sha256");
        ValidateSafeEntry(manifestEntry);
        ValidateSafeEntry(snapshotEntry);
        ValidateSafeEntry(checksumsEntry);

        if (snapshotEntry.Length > MaximumSnapshotBytes)
        {
            throw new InvalidDataException("Распакованный снимок превышает допустимый размер 1 ГБ.");
        }

        byte[] manifestBytes = await ReadEntryAsync(manifestEntry, MaximumArchiveBytes, cancellationToken);
        byte[] snapshotBytes = await ReadEntryAsync(snapshotEntry, MaximumSnapshotBytes, cancellationToken);
        byte[] checksumBytes = await ReadEntryAsync(checksumsEntry, 64 * 1024, cancellationToken);
        VerifyChecksums(manifestBytes, snapshotBytes, checksumBytes);

        SnapshotArchiveManifest? manifest = JsonSerializer.Deserialize<SnapshotArchiveManifest>(
            manifestBytes,
            JsonOptions);
        if (manifest is null || manifest.FormatVersion != 1 || manifest.SchemaVersion > 1)
        {
            throw new InvalidDataException("Версия формата или схемы снимка не поддерживается.");
        }

        SnapshotRecord? snapshot = JsonSerializer.Deserialize<SnapshotRecord>(snapshotBytes, JsonOptions);
        if (snapshot is null || snapshot.Id != manifest.SnapshotId)
        {
            throw new InvalidDataException("Содержимое snapshot.json не соответствует manifest.json.");
        }

        await _store.SaveSnapshotAsync(snapshot, cancellationToken);
        return snapshot;
    }

    private static async Task WriteEntryAsync(
        ZipArchive archive,
        string name,
        byte[] content,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        await stream.WriteAsync(content, cancellationToken);
    }

    private static ZipArchiveEntry GetRequiredEntry(ZipArchive archive, string name) =>
        archive.GetEntry(name)
        ?? throw new InvalidDataException($"В архиве отсутствует {name}.");

    private static void ValidateSafeEntry(ZipArchiveEntry entry)
    {
        if (!string.Equals(entry.FullName, entry.Name, StringComparison.Ordinal)
            || entry.FullName.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(entry.FullName))
        {
            throw new InvalidDataException("Архив содержит небезопасный путь.");
        }
    }

    private static async Task<byte[]> ReadEntryAsync(
        ZipArchiveEntry entry,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (entry.Length > maximumBytes)
        {
            throw new InvalidDataException($"Запись {entry.FullName} слишком большая.");
        }

        await using Stream input = entry.Open();
        using var output = new MemoryStream(capacity: checked((int)Math.Min(entry.Length, int.MaxValue)));
        byte[] buffer = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maximumBytes)
            {
                throw new InvalidDataException($"Запись {entry.FullName} превышает лимит.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return output.ToArray();
    }

    private static void VerifyChecksums(
        byte[] manifestBytes,
        byte[] snapshotBytes,
        byte[] checksumBytes)
    {
        string checksums = Encoding.ASCII.GetString(checksumBytes);
        string manifestHash = Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant();
        string snapshotHash = Convert.ToHexString(SHA256.HashData(snapshotBytes)).ToLowerInvariant();
        if (!checksums.Contains($"{manifestHash}  manifest.json", StringComparison.OrdinalIgnoreCase)
            || !checksums.Contains($"{snapshotHash}  snapshot.json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Контрольные суммы архива не совпадают.");
        }
    }

    private sealed record SnapshotArchiveManifest(
        string Format,
        int FormatVersion,
        int SchemaVersion,
        string SysDiffVersion,
        Guid SnapshotId,
        DateTimeOffset CreatedAtUtc);
}
