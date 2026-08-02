using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SysDiff.Domain;
using SysDiff.Reporting;
using SysDiff.Storage;

namespace SysDiff.Cli;

internal sealed class InvestigationBundleService
{
    private readonly ISnapshotStore _store;
    private readonly SnapshotArchiveService _snapshotArchive;
    private readonly SchemaContractService _schemaContracts;
    private readonly JsonReportRenderer _jsonRenderer;
    private readonly MarkdownReportRenderer _markdownRenderer;
    private readonly HtmlReportRenderer _htmlRenderer;

    public InvestigationBundleService(
        ISnapshotStore store,
        SnapshotArchiveService snapshotArchive,
        SchemaContractService schemaContracts,
        JsonReportRenderer jsonRenderer,
        MarkdownReportRenderer markdownRenderer,
        HtmlReportRenderer htmlRenderer)
    {
        _store = store;
        _snapshotArchive = snapshotArchive;
        _schemaContracts = schemaContracts;
        _jsonRenderer = jsonRenderer;
        _markdownRenderer = markdownRenderer;
        _htmlRenderer = htmlRenderer;
    }

    public async Task<string> CreateAsync(
        Guid comparisonId,
        string outputPath,
        CancellationToken cancellationToken)
    {
        ComparisonResult comparison = await _store.GetComparisonAsync(comparisonId, cancellationToken)
            ?? throw new InvalidOperationException("Сравнение не найдено.");
        SnapshotRecord before = await _store.GetSnapshotAsync(
            comparison.BeforeSnapshotId.ToString(),
            cancellationToken)
            ?? throw new InvalidOperationException("Начальный снимок сравнения не найден.");
        SnapshotRecord after = await _store.GetSnapshotAsync(
            comparison.AfterSnapshotId.ToString(),
            cancellationToken)
            ?? throw new InvalidOperationException("Итоговый снимок сравнения не найден.");

        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
        string working = Path.Combine(Path.GetTempPath(), $"sysdiff-bundle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(working);

        try
        {
            await _snapshotArchive.ExportAsync(
                before,
                Path.Combine(working, "before.sdshot"),
                cancellationToken);
            await _snapshotArchive.ExportAsync(
                after,
                Path.Combine(working, "after.sdshot"),
                cancellationToken);

            await File.WriteAllTextAsync(
                Path.Combine(working, "report.json"),
                _jsonRenderer.Render(before, after, comparison),
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(working, "report.md"),
                _markdownRenderer.Render(before, after, comparison),
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(working, "report.html"),
                _htmlRenderer.Render(before, after, comparison),
                cancellationToken);

            var manifest = new
            {
                format = "SysDiff Investigation Bundle",
                formatVersion = SysDiffProduct.InvestigationBundleFormatVersion,
                schemaVersion = SysDiffProduct.PublicSchemaVersion,
                sysDiffVersion = SysDiffProduct.Version,
                createdAtUtc = DateTimeOffset.UtcNow,
                comparisonId = comparison.Id,
                beforeSnapshotId = before.Id,
                afterSnapshotId = after.Id,
                crossMachine = comparison.CrossMachine,
                warnings = comparison.Warnings,
                privacy = new
                {
                    userProfilePathsRedacted = true,
                    privateKeysIncluded = false,
                    rawLogsIncluded = false
                }
            };
            string manifestJson = JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions { WriteIndented = true });
            SchemaValidationResult manifestValidation = _schemaContracts.ValidateJson(
                SchemaContractKind.InvestigationBundleManifest,
                manifestJson,
                "manifest.json");
            if (!manifestValidation.IsValid)
            {
                string issues = string.Join(
                    "; ",
                    manifestValidation.Issues.Select(value =>
                        $"{value.Path} [{value.Code}] {value.Message}"));
                throw new InvalidDataException(
                    $"Bundle manifest не соответствует Schema Contract v1: {issues}");
            }

            await File.WriteAllTextAsync(
                Path.Combine(working, "manifest.json"),
                manifestJson,
                cancellationToken);

            string[] payloadFiles = Directory.GetFiles(working)
                .OrderBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var checksums = new StringBuilder();
            foreach (string file in payloadFiles)
            {
                await using FileStream stream = File.OpenRead(file);
                byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
                checksums.Append(Convert.ToHexString(hash).ToLowerInvariant())
                    .Append("  ")
                    .AppendLine(Path.GetFileName(file));
            }

            await File.WriteAllTextAsync(
                Path.Combine(working, "checksums.sha256"),
                checksums.ToString(),
                Encoding.ASCII,
                cancellationToken);

            string temporary = fullPath + $".{Guid.NewGuid():N}.tmp";
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            ZipFile.CreateFromDirectory(
                working,
                temporary,
                CompressionLevel.Optimal,
                includeBaseDirectory: false);
            File.Move(temporary, fullPath, overwrite: true);
            return fullPath;
        }
        finally
        {
            if (Directory.Exists(working))
            {
                Directory.Delete(working, recursive: true);
            }
        }
    }
}
