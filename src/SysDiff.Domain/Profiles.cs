namespace SysDiff.Domain;

public sealed record ProviderOptions
{
    public bool Enabled { get; init; } = true;

    public List<string> Roots { get; init; } = [];

    public List<string> Exclude { get; init; } = [];

    public HashMode HashMode { get; init; } = HashMode.Smart;

    public int MaximumDepth { get; init; } = 8;

    public long MaximumFileSizeBytes { get; init; } = 512L * 1024L * 1024L;

    public int MaximumArtifacts { get; init; } = 500_000;

    public Dictionary<string, string> Settings { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed record CaptureProfile
{
    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public Dictionary<string, ProviderOptions> Providers { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed record SnapshotContext
{
    public required CaptureProfile Profile { get; init; }

    public required string DataDirectory { get; init; }

    public bool IsAdministrator { get; init; }

    public IProgress<SnapshotProgress>? Progress { get; init; }
}

public sealed record SnapshotProgress(
    string ProviderId,
    string Message,
    long Processed,
    string? CurrentItem = null);
