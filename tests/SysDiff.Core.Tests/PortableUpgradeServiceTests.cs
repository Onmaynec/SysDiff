using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SysDiff.Storage;

namespace SysDiff.Core.Tests;

public sealed class PortableUpgradeServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"sysdiff-legacy-tests-{Guid.NewGuid():N}");

    public PortableUpgradeServiceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task LegacyComparison_PlanAndConvert_CreateBackupAndValidV1()
    {
        string input = Path.Combine(_directory, "legacy-report.json");
        string output = Path.Combine(_directory, "report-v1.json");
        byte[] source = await File.ReadAllBytesAsync(LegacyFixturePath());
        await File.WriteAllBytesAsync(input, source);
        var service = CreateService();

        PortableUpgradePlan plan = await service.PlanAsync(
            PortableUpgradeKind.ComparisonReport,
            input,
            CancellationToken.None);

        Assert.Equal(PortableUpgradeStatus.UpgradeAvailable, plan.Status);
        Assert.True(plan.CanConvert);
        Assert.True(plan.RequiresBackup);
        Assert.Contains(plan.Steps, value =>
            value.Id == "0.11.0-comparison-contract-v1");

        PortableUpgradeResult result = await service.ConvertAsync(
            PortableUpgradeKind.ComparisonReport,
            input,
            output,
            overwrite: false,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Changed);
        Assert.Equal(PortableUpgradeStatus.Current, result.StatusAfter);
        Assert.True(File.Exists(output));
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));
        Assert.Equal(source, await File.ReadAllBytesAsync(result.BackupPath!));

        JsonObject converted = JsonNode.Parse(await File.ReadAllTextAsync(output))!.AsObject();
        Assert.Equal("SysDiff Comparison Report", converted["format"]!.GetValue<string>());
        Assert.Equal(1, converted["formatVersion"]!.GetValue<int>());
        Assert.Equal(1, converted["schemaVersion"]!.GetValue<int>());
        Assert.Equal("0.0.0-legacy", converted["sysDiffVersion"]!.GetValue<string>());
        Assert.True(converted["legacyRootExtension"]!["preserve"]!.GetValue<bool>());
        Assert.Equal(
            "preserve",
            converted["comparison"]!["changes"]![0]!["legacyChangeExtension"]!.GetValue<string>());

        PortableUpgradePlan after = await service.PlanAsync(
            PortableUpgradeKind.ComparisonReport,
            output,
            CancellationToken.None);
        Assert.True(after.IsCurrent);
    }

    [Fact]
    public async Task CurrentComparison_RepeatedConvert_IsSafeNoOp()
    {
        string input = Path.Combine(_directory, "legacy.json");
        string output = Path.Combine(_directory, "current.json");
        File.Copy(LegacyFixturePath(), input);
        var service = CreateService();
        PortableUpgradeResult first = await service.ConvertAsync(
            PortableUpgradeKind.ComparisonReport,
            input,
            output,
            overwrite: false,
            CancellationToken.None);
        Assert.True(first.Success);

        PortableUpgradeResult second = await service.ConvertAsync(
            PortableUpgradeKind.ComparisonReport,
            output,
            null,
            overwrite: false,
            CancellationToken.None);

        Assert.True(second.Success);
        Assert.False(second.Changed);
        Assert.Null(second.BackupPath);
        Assert.Equal(PortableUpgradeStatus.Current, second.StatusAfter);
    }

    [Fact]
    public async Task FutureComparison_RequiresNewerSysDiff()
    {
        JsonObject root = JsonNode.Parse(
            await File.ReadAllTextAsync(LegacyFixturePath()))!.AsObject();
        root["schemaVersion"] = 2;
        string input = Path.Combine(_directory, "future.json");
        await File.WriteAllTextAsync(input, root.ToJsonString());
        var service = CreateService();

        PortableUpgradePlan plan = await service.PlanAsync(
            PortableUpgradeKind.ComparisonReport,
            input,
            CancellationToken.None);

        Assert.Equal(PortableUpgradeStatus.RequiresNewerSysDiff, plan.Status);
        Assert.False(plan.CanConvert);
    }

    [Fact]
    public async Task LegacyBundle_Convert_PreservesSnapshotsAndRebuildsChecksums()
    {
        string input = Path.Combine(_directory, "legacy-bundle.zip");
        string output = Path.Combine(_directory, "bundle-v1.zip");
        byte[] before = Encoding.UTF8.GetBytes("before-snapshot-byte-for-byte");
        byte[] after = Encoding.UTF8.GetBytes("after-snapshot-byte-for-byte");
        await CreateLegacyBundleAsync(input, before, after, tamperReportAfterChecksums: false);
        byte[] sourceBundle = await File.ReadAllBytesAsync(input);
        var service = CreateService();

        PortableUpgradePlan plan = await service.PlanAsync(
            PortableUpgradeKind.InvestigationBundle,
            input,
            CancellationToken.None);
        Assert.Equal(PortableUpgradeStatus.UpgradeAvailable, plan.Status);
        Assert.Equal(3, plan.Steps.Count);

        PortableUpgradeResult result = await service.ConvertAsync(
            PortableUpgradeKind.InvestigationBundle,
            input,
            output,
            overwrite: false,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Changed);
        Assert.NotNull(result.BackupPath);
        Assert.Equal(sourceBundle, await File.ReadAllBytesAsync(result.BackupPath!));

        Dictionary<string, byte[]> entries = ReadZip(output);
        Assert.Equal(before, entries["before.sdshot"]);
        Assert.Equal(after, entries["after.sdshot"]);
        JsonObject manifest = JsonNode.Parse(entries["manifest.json"])!.AsObject();
        JsonObject report = JsonNode.Parse(entries["report.json"])!.AsObject();
        Assert.Equal(1, manifest["schemaVersion"]!.GetValue<int>());
        Assert.Equal("SysDiff Comparison Report", report["format"]!.GetValue<string>());
        Assert.Equal("0.0.0-legacy", report["sysDiffVersion"]!.GetValue<string>());

        PortableUpgradePlan afterPlan = await service.PlanAsync(
            PortableUpgradeKind.InvestigationBundle,
            output,
            CancellationToken.None);
        Assert.True(afterPlan.IsCurrent);
    }

    [Fact]
    public async Task BundleWithTamperedChecksum_IsRejectedBeforeBackup()
    {
        string input = Path.Combine(_directory, "tampered.zip");
        await CreateLegacyBundleAsync(
            input,
            Encoding.UTF8.GetBytes("before"),
            Encoding.UTF8.GetBytes("after"),
            tamperReportAfterChecksums: true);
        var service = CreateService();

        PortableUpgradePlan plan = await service.PlanAsync(
            PortableUpgradeKind.InvestigationBundle,
            input,
            CancellationToken.None);
        PortableUpgradeResult result = await service.ConvertAsync(
            PortableUpgradeKind.InvestigationBundle,
            input,
            Path.Combine(_directory, "should-not-exist.zip"),
            overwrite: false,
            CancellationToken.None);

        Assert.Equal(PortableUpgradeStatus.Invalid, plan.Status);
        Assert.False(result.Success);
        Assert.Null(result.BackupPath);
        Assert.Empty(Directory.GetFiles(_directory, "*.legacy-backup-*"));
    }

    private static PortableUpgradeService CreateService() =>
        new(new SchemaContractService());

    private static string LegacyFixturePath() =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "legacy",
            "v0.9",
            "comparison-report.legacy.json");

    private static async Task CreateLegacyBundleAsync(
        string path,
        byte[] before,
        byte[] after,
        bool tamperReportAfterChecksums)
    {
        byte[] report = await File.ReadAllBytesAsync(LegacyFixturePath());
        byte[] manifest = Encoding.UTF8.GetBytes(
            """
            {
              "format": "SysDiff Investigation Bundle",
              "formatVersion": 1,
              "sysDiffVersion": "0.3.0",
              "createdAtUtc": "2026-08-02T08:05:00+00:00",
              "comparisonId": "33333333-3333-3333-3333-333333333333",
              "beforeSnapshotId": "11111111-1111-1111-1111-111111111111",
              "afterSnapshotId": "22222222-2222-2222-2222-222222222222",
              "crossMachine": false,
              "warnings": [],
              "privacy": {
                "userProfilePathsRedacted": true,
                "privateKeysIncluded": false,
                "rawLogsIncluded": false
              },
              "legacyBundleExtension": "preserve"
            }
            """);
        var payload = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["manifest.json"] = manifest,
            ["report.json"] = report,
            ["report.md"] = Encoding.UTF8.GetBytes("# Legacy report"),
            ["report.html"] = Encoding.UTF8.GetBytes("<h1>Legacy report</h1>"),
            ["before.sdshot"] = before,
            ["after.sdshot"] = after
        };
        byte[] checksums = CreateChecksums(payload);
        if (tamperReportAfterChecksums)
        {
            payload["report.json"] = Encoding.UTF8.GetBytes("{\"tampered\":true}");
        }
        payload["checksums.sha256"] = checksums;

        await using FileStream file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false);
        foreach ((string name, byte[] content) in payload)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            await using Stream output = entry.Open();
            await output.WriteAsync(content);
        }
    }

    private static byte[] CreateChecksums(IReadOnlyDictionary<string, byte[]> payload)
    {
        var builder = new StringBuilder();
        foreach ((string name, byte[] content) in payload
                     .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant())
                .Append("  ")
                .Append(name)
                .Append('\n');
        }
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static Dictionary<string, byte[]> ReadZip(string path)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            using Stream input = entry.Open();
            using var output = new MemoryStream();
            input.CopyTo(output);
            result[entry.FullName] = output.ToArray();
        }
        return result;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
