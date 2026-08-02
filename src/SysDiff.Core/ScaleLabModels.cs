using SysDiff.Domain;

namespace SysDiff.Core;

public enum ScaleInputStatus
{
    ValidSorted,
    ValidUnsorted,
    Invalid
}

public sealed record ScaleProgress
{
    public long Processed { get; init; }

    public long Written { get; init; }

    public long BytesRead { get; init; }

    public long ManagedBytes { get; init; }

    public long WorkingSetBytes { get; init; }
}

public sealed record ScaleSyntheticOptions
{
    public int Count { get; init; } = 1_000_000;

    public string Variant { get; init; } = "before";

    public int ChangeEvery { get; init; } = 1_000;
}

public sealed record ScaleSyntheticResult
{
    public required string OutputPath { get; init; }

    public int ArtifactCount { get; init; }

    public long SizeBytes { get; init; }

    public string Variant { get; init; } = string.Empty;

    public TimeSpan Duration { get; init; }
}

public sealed record ScaleSortOptions
{
    public int BatchSize { get; init; } = 50_000;

    public int ProgressInterval { get; init; } = 100_000;
}

public sealed record ScaleSortResult
{
    public required string InputPath { get; init; }

    public required string OutputPath { get; init; }

    public long ArtifactCount { get; init; }

    public int ChunkCount { get; init; }

    public long PeakManagedBytes { get; init; }

    public long PeakWorkingSetBytes { get; init; }

    public TimeSpan Duration { get; init; }
}

public sealed record ScaleCompareOptions
{
    public int ProgressInterval { get; init; } = 100_000;

    public bool IncludeUnchanged { get; init; }
}

public sealed record ScaleChangeRecord
{
    public required ChangeType ChangeType { get; init; }

    public required string Identity { get; init; }

    public required string ProviderId { get; init; }

    public required string ArtifactType { get; init; }

    public required string DisplayName { get; init; }

    public SystemArtifact? Before { get; init; }

    public SystemArtifact? After { get; init; }

    public List<PropertyChange> ChangedProperties { get; init; } = [];
}

public sealed record ScaleComparisonResult
{
    public required string BeforePath { get; init; }

    public required string AfterPath { get; init; }

    public required string OutputPath { get; init; }

    public long BeforeArtifacts { get; init; }

    public long AfterArtifacts { get; init; }

    public long ComparedIdentities { get; init; }

    public long Added { get; init; }

    public long Removed { get; init; }

    public long Modified { get; init; }

    public long Unchanged { get; init; }

    public long WrittenChanges { get; init; }

    public long PeakManagedBytes { get; init; }

    public long PeakWorkingSetBytes { get; init; }

    public double ThroughputArtifactsPerSecond { get; init; }

    public TimeSpan Duration { get; init; }
}

public sealed record ScaleBenchmarkOptions
{
    public int ArtifactCount { get; init; } = 1_000_000;

    public int ChangeEvery { get; init; } = 1_000;

    public int BatchSize { get; init; } = 50_000;

    public int MaxManagedMemoryMb { get; init; } = 256;

    public double MinimumThroughputArtifactsPerSecond { get; init; } = 1_000;
}

public sealed record ScaleBenchmarkResult
{
    public required string ProductVersion { get; init; }

    public required string OutputDirectory { get; init; }

    public required string ResultPath { get; init; }

    public int ArtifactCount { get; init; }

    public int ExpectedModified { get; init; }

    public long ActualModified { get; init; }

    public long PeakManagedBytes { get; init; }

    public long PeakWorkingSetBytes { get; init; }

    public double ThroughputArtifactsPerSecond { get; init; }

    public int MaxManagedMemoryMb { get; init; }

    public double MinimumThroughputArtifactsPerSecond { get; init; }

    public bool MemoryPassed { get; init; }

    public bool ThroughputPassed { get; init; }

    public bool CountPassed { get; init; }

    public bool Passed => MemoryPassed && ThroughputPassed && CountPassed;

    public TimeSpan Duration { get; init; }
}
