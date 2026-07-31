namespace SysDiff.Domain;

public enum SnapshotStatus
{
    InProgress,
    Completed,
    Partial,
    Failed,
    Cancelled,
    Corrupted
}

public enum ProviderStatus
{
    Success,
    Partial,
    Failed,
    Skipped,
    Cancelled
}

public enum ChangeType
{
    Added,
    Removed,
    Modified,
    Unchanged,
    Moved,
    Renamed,
    Unknown
}

public enum Severity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum NoiseMode
{
    Raw,
    Balanced,
    Strict
}

public enum HashMode
{
    None,
    MetadataOnly,
    Smart,
    Full
}
