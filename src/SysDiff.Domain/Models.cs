using System.Text.Json.Serialization;

namespace SysDiff.Domain;

public sealed record ArtifactValue
{
    public string? Value { get; init; }

    public string Type { get; init; } = "string";

    public bool Redacted { get; init; }

    public string? Hash { get; init; }

    public static ArtifactValue From(object? value, string? type = null) =>
        new()
        {
            Value = value?.ToString(),
            Type = type ?? value?.GetType().Name ?? "null"
        };
}

public sealed record SystemArtifact
{
    public required string ProviderId { get; init; }

    public required string ArtifactType { get; init; }

    public required string Identity { get; init; }

    public required string DisplayName { get; init; }

    public Dictionary<string, ArtifactValue> Properties { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Tags { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed record ProviderSnapshotResult
{
    public required string ProviderId { get; init; }

    public required string DisplayName { get; init; }

    public ProviderStatus Status { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset FinishedAtUtc { get; init; }

    [JsonIgnore]
    public TimeSpan Duration => FinishedAtUtc - StartedAtUtc;

    public int ArtifactCount { get; init; }

    public List<string> Warnings { get; init; } = [];

    public List<string> Errors { get; init; } = [];

    public List<SystemArtifact> Artifacts { get; init; } = [];

    public bool RequiresAdministrator { get; init; }
}

public sealed record SnapshotRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string SysDiffVersion { get; init; } = "0.4.0";

    public int SchemaVersion { get; init; } = 1;

    public string ProfileName { get; init; } = "standard";

    public SnapshotStatus Status { get; init; } = SnapshotStatus.InProgress;

    public string? WindowsEdition { get; init; }

    public string? WindowsBuild { get; init; }

    public string Architecture { get; init; } = Environment.Is64BitOperatingSystem ? "x64" : "x86";

    public string? MachineFingerprint { get; init; }

    public string? Comment { get; init; }

    public List<ProviderSnapshotResult> ProviderResults { get; init; } = [];

    public List<SystemArtifact> Artifacts { get; init; } = [];
}

public sealed record PropertyChange
{
    public required string Name { get; init; }

    public ArtifactValue? Before { get; init; }

    public ArtifactValue? After { get; init; }
}

public sealed record SystemChange
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required ChangeType ChangeType { get; init; }

    public required string ProviderId { get; init; }

    public required string ArtifactType { get; init; }

    public required string Identity { get; init; }

    public required string DisplayName { get; init; }

    public SystemArtifact? Before { get; init; }

    public SystemArtifact? After { get; init; }

    public List<PropertyChange> ChangedProperties { get; init; } = [];

    public Severity Severity { get; init; } = Severity.Info;

    public string Explanation { get; init; } = string.Empty;

    public string WhyThisMatters { get; init; } = string.Empty;

    public HashSet<string> Tags { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public double Confidence { get; init; } = 1.0;

    public bool IsNoise { get; init; }
}

public sealed record ComparisonResult
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid BeforeSnapshotId { get; init; }

    public required Guid AfterSnapshotId { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public NoiseMode NoiseMode { get; init; } = NoiseMode.Balanced;

    public bool CrossMachine { get; init; }

    public List<string> Warnings { get; init; } = [];

    public List<SystemChange> Changes { get; init; } = [];

    public int HiddenAsNoise { get; init; }
}

public sealed record LiveEvent
{
    public required DateTimeOffset TimestampUtc { get; init; }

    public required string Category { get; init; }

    public required string EventType { get; init; }

    public required string Identity { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public Dictionary<string, string?> Properties { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
