using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SysDiff.Domain;
using SysDiff.Storage;

namespace SysDiff.Core.Tests;

public sealed class SnapshotArchiveTests
{
    [Fact]
    public async Task ExportImport_RoundTripPreservesArtifacts()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sdshot");
        var store = new MemoryStore();
        var service = new SnapshotArchiveService(store);
        var snapshot = new SnapshotRecord
        {
            Name = "roundtrip",
            Status = SnapshotStatus.Completed,
            Artifacts =
            [
                new SystemArtifact
                {
                    ProviderId = "test",
                    ArtifactType = "Item",
                    Identity = "test://one",
                    DisplayName = "One",
                    Properties = new Dictionary<string, ArtifactValue>
                    {
                        ["Value"] = ArtifactValue.From("demo")
                    }
                }
            ]
        };

        try
        {
            await service.ExportAsync(snapshot, path, CancellationToken.None);
            SnapshotRecord imported = await service.ImportAsync(path, CancellationToken.None);

            Assert.Equal(snapshot.Id, imported.Id);
            Assert.Equal("test://one", Assert.Single(imported.Artifacts).Identity);
            Assert.NotNull(store.Snapshot);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Inspect_CompatibleArchiveDoesNotWriteToStore()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sdshot");
        var store = new MemoryStore();
        var service = new SnapshotArchiveService(store);
        var snapshot = new SnapshotRecord
        {
            Name = "inspect",
            Status = SnapshotStatus.Completed
        };

        try
        {
            await service.ExportAsync(snapshot, path, CancellationToken.None);
            SnapshotArchiveInspection inspection = await service.InspectAsync(
                path,
                CancellationToken.None);

            Assert.Equal(SnapshotArchiveCompatibilityStatus.Compatible, inspection.Status);
            Assert.True(inspection.ChecksumsValid);
            Assert.True(inspection.CanImport);
            Assert.Equal(snapshot.Id, inspection.SnapshotId);
            Assert.Null(store.Snapshot);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Inspect_FutureSchemaIsRejectedWithoutPartialImport()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sdshot");
        var store = new MemoryStore();
        var service = new SnapshotArchiveService(store);
        var snapshot = new SnapshotRecord
        {
            Name = "future",
            Status = SnapshotStatus.Completed
        };

        try
        {
            await service.ExportAsync(snapshot, path, CancellationToken.None);
            await RewriteSchemaVersionAsync(path, 2);

            SnapshotArchiveInspection inspection = await service.InspectAsync(
                path,
                CancellationToken.None);

            Assert.Equal(
                SnapshotArchiveCompatibilityStatus.RequiresNewerSysDiff,
                inspection.Status);
            Assert.True(inspection.ChecksumsValid);
            Assert.False(inspection.CanImport);
            Assert.Null(store.Snapshot);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.ImportAsync(path, CancellationToken.None));
            Assert.Null(store.Snapshot);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Import_RejectsModifiedChecksumFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sdshot");
        var store = new MemoryStore();
        var service = new SnapshotArchiveService(store);
        var snapshot = new SnapshotRecord
        {
            Name = "corrupted",
            Status = SnapshotStatus.Completed
        };

        try
        {
            await service.ExportAsync(snapshot, path, CancellationToken.None);
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Update))
            {
                ZipArchiveEntry entry = archive.GetEntry("checksums.sha256")!;
                entry.Delete();
                ZipArchiveEntry replacement = archive.CreateEntry("checksums.sha256");
                await using Stream writer = replacement.Open();
                await writer.WriteAsync("invalid"u8.ToArray());
            }

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.ImportAsync(path, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task RewriteSchemaVersionAsync(string path, int schemaVersion)
    {
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Update);

        ZipArchiveEntry manifestEntry = archive.GetEntry("manifest.json")!;
        ZipArchiveEntry snapshotEntry = archive.GetEntry("snapshot.json")!;
        byte[] manifestBytes = await ReadEntryAsync(manifestEntry);
        byte[] snapshotBytes = await ReadEntryAsync(snapshotEntry);

        JsonObject manifest = JsonNode.Parse(manifestBytes)!.AsObject();
        JsonObject snapshot = JsonNode.Parse(snapshotBytes)!.AsObject();
        manifest["SchemaVersion"] = schemaVersion;
        snapshot["SchemaVersion"] = schemaVersion;

        manifestBytes = Encoding.UTF8.GetBytes(manifest.ToJsonString(JsonOptions));
        snapshotBytes = Encoding.UTF8.GetBytes(snapshot.ToJsonString(JsonOptions));
        byte[] checksums = Encoding.ASCII.GetBytes(
            $"{Hash(manifestBytes)}  manifest.json\n{Hash(snapshotBytes)}  snapshot.json\n");

        ReplaceEntry(archive, manifestEntry, "manifest.json", manifestBytes);
        ReplaceEntry(archive, snapshotEntry, "snapshot.json", snapshotBytes);
        ReplaceEntry(
            archive,
            archive.GetEntry("checksums.sha256")!,
            "checksums.sha256",
            checksums);
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry)
    {
        await using Stream input = entry.Open();
        using var output = new MemoryStream();
        await input.CopyToAsync(output);
        return output.ToArray();
    }

    private static void ReplaceEntry(
        ZipArchive archive,
        ZipArchiveEntry entry,
        string name,
        byte[] content)
    {
        entry.Delete();
        ZipArchiveEntry replacement = archive.CreateEntry(name, CompressionLevel.Optimal);
        using Stream output = replacement.Open();
        output.Write(content);
    }

    private static string Hash(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private sealed class MemoryStore : ISnapshotStore
    {
        public SnapshotRecord? Snapshot { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveSnapshotAsync(SnapshotRecord snapshot, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SnapshotRecord>> ListSnapshotsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SnapshotRecord>>(
                Snapshot is null ? [] : [Snapshot]);

        public Task<SnapshotRecord?> GetSnapshotAsync(
            string nameOrId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Snapshot);

        public Task DeleteSnapshotAsync(string nameOrId, CancellationToken cancellationToken)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }

        public Task SaveComparisonAsync(
            ComparisonResult comparison,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ComparisonResult?> GetComparisonAsync(
            Guid id,
            CancellationToken cancellationToken) => Task.FromResult<ComparisonResult?>(null);
    }
}
