namespace SysDiff.Storage;

public enum PortableUpgradeKind
{
    ComparisonReport,
    InvestigationBundle
}

public enum PortableUpgradeStatus
{
    Current,
    UpgradeAvailable,
    RequiresNewerSysDiff,
    UnsupportedLegacy,
    Invalid
}

public sealed record PortableUpgradeStep
{
    public required string Id { get; init; }

    public required string Description { get; init; }

    public int TargetSchemaVersion { get; init; } = 1;

    public bool Destructive { get; init; }
}

public sealed record PortableUpgradePlan
{
    public required PortableUpgradeKind Kind { get; init; }

    public required string InputPath { get; init; }

    public required PortableUpgradeStatus Status { get; init; }

    public required string SourceShape { get; init; }

    public required string SuggestedOutputPath { get; init; }

    public int TargetSchemaVersion { get; init; } = 1;

    public bool RequiresBackup { get; init; }

    public List<PortableUpgradeStep> Steps { get; init; } = [];

    public List<string> Warnings { get; init; } = [];

    public required string Message { get; init; }

    public bool CanConvert => Status == PortableUpgradeStatus.UpgradeAvailable;

    public bool IsCurrent => Status == PortableUpgradeStatus.Current;
}

public sealed record PortableUpgradeResult
{
    public required PortableUpgradeKind Kind { get; init; }

    public required string InputPath { get; init; }

    public string? OutputPath { get; init; }

    public string? BackupPath { get; init; }

    public bool Success { get; init; }

    public bool Changed { get; init; }

    public PortableUpgradeStatus StatusAfter { get; init; }

    public string? SourceSha256 { get; init; }

    public string? OutputSha256 { get; init; }

    public List<string> AppliedStepIds { get; init; } = [];

    public required string Message { get; init; }
}
