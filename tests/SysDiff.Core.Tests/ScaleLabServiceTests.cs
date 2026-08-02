using System.Text.Json;
using SysDiff.Core;

namespace SysDiff.Core.Tests;

public sealed class ScaleLabServiceTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"sysdiff-scale-tests-{Guid.NewGuid():N}");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SyntheticComparison_StreamsExpectedChanges()
    {
        var service = new ScaleLabService();
        string before = Path.Combine(_directory, "before.ndjson");
        string after = Path.Combine(_directory, "after.ndjson");
        string changes = Path.Combine(_directory, "changes.ndjson");

        await service.GenerateSyntheticAsync(
            before,
            new ScaleSyntheticOptions
            {
                Count = 10_000,
                Variant = "before",
                ChangeEvery = 100
            },
            progress: null,
            CancellationToken.None);
        await service.GenerateSyntheticAsync(
            after,
            new ScaleSyntheticOptions
            {
                Count = 10_000,
                Variant = "after",
                ChangeEvery = 100
            },
            progress: null,
            CancellationToken.None);

        ScaleComparisonResult result = await service.CompareAsync(
            before,
            after,
            changes,
            new ScaleCompareOptions(),
            progress: null,
            CancellationToken.None);

        Assert.Equal(10_000, result.BeforeArtifacts);
        Assert.Equal(10_000, result.AfterArtifacts);
        Assert.Equal(100, result.Modified);
        Assert.Equal(9_900, result.Unchanged);
        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(100, result.WrittenChanges);
        Assert.Equal(100, File.ReadLines(changes).Count());
        Assert.True(result.PeakManagedBytes < 128L * 1024 * 1024);
    }

    [Fact]
    public async Task ExternalSort_OrdersByIdentityWithoutMaterializingOutput()
    {
        var service = new ScaleLabService();
        string input = Path.Combine(_directory, "unsorted.ndjson");
        string output = Path.Combine(_directory, "sorted.ndjson");
        await File.WriteAllLinesAsync(input,
        [
            Artifact("synthetic://artifact/000000003"),
            Artifact("synthetic://artifact/000000001"),
            Artifact("synthetic://artifact/000000004"),
            Artifact("synthetic://artifact/000000002")
        ]);

        ScaleSortResult result = await service.SortAsync(
            input,
            output,
            new ScaleSortOptions { BatchSize = 1_000 },
            progress: null,
            CancellationToken.None);

        string[] identities = File.ReadLines(output)
            .Select(ReadIdentity)
            .ToArray();
        Assert.Equal(
        [
            "synthetic://artifact/000000001",
            "synthetic://artifact/000000002",
            "synthetic://artifact/000000003",
            "synthetic://artifact/000000004"
        ],
            identities);
        Assert.Equal(4, result.ArtifactCount);
        Assert.Equal(1, result.ChunkCount);
    }

    [Fact]
    public async Task Comparison_RejectsUnsortedInput()
    {
        var service = new ScaleLabService();
        string before = Path.Combine(_directory, "before-unsorted.ndjson");
        string after = Path.Combine(_directory, "after.ndjson");
        string changes = Path.Combine(_directory, "changes.ndjson");
        await File.WriteAllLinesAsync(before,
        [
            Artifact("synthetic://artifact/2"),
            Artifact("synthetic://artifact/1")
        ]);
        await File.WriteAllLinesAsync(after,
        [
            Artifact("synthetic://artifact/1"),
            Artifact("synthetic://artifact/2")
        ]);

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CompareAsync(
                before,
                after,
                changes,
                new ScaleCompareOptions(),
                progress: null,
                CancellationToken.None));

        Assert.Contains("scale sort", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(changes));
    }

    [Fact]
    public async Task Benchmark_ProducesMachineReadableRegressionResult()
    {
        var service = new ScaleLabService();
        string output = Path.Combine(_directory, "benchmark");

        ScaleBenchmarkResult result = await service.RunBenchmarkAsync(
            output,
            new ScaleBenchmarkOptions
            {
                ArtifactCount = 20_000,
                ChangeEvery = 200,
                MaxManagedMemoryMb = 128,
                MinimumThroughputArtifactsPerSecond = 1
            },
            progress: null,
            CancellationToken.None);

        Assert.True(result.Passed);
        Assert.Equal(100, result.ExpectedModified);
        Assert.Equal(100, result.ActualModified);
        Assert.True(File.Exists(result.ResultPath));
        using JsonDocument document = JsonDocument.Parse(
            await File.ReadAllTextAsync(result.ResultPath));
        Assert.True(document.RootElement.GetProperty("passed").GetBoolean());
    }

    private static string Artifact(string identity) =>
        $$"""
        {"providerId":"synthetic.scale","artifactType":"ScaleArtifact","identity":"{{identity}}","displayName":"{{identity}}","properties":{},"tags":[]}
        """;

    private static string ReadIdentity(string line)
    {
        using JsonDocument document = JsonDocument.Parse(line);
        return document.RootElement.GetProperty("identity").GetString()!;
    }
}
