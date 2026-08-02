namespace SysDiff.Storage;

public enum DatabaseCompatibilityStatus
{
    Current,
    MigrationRequired,
    RequiresNewerSysDiff,
    Invalid
}

public enum DatabaseMigrationRunStatus
{
    Applied,
    Failed
}

public sealed record DatabaseMigrationDescriptor
{
    public required string Id { get; init; }

    public required string TargetVersion { get; init; }

    public required int UserVersion { get; init; }

    public required string Description { get; init; }

    public bool Destructive { get; init; }

    public bool RequiresBackup { get; init; } = true;
}

public sealed record DatabaseMigrationHistoryEntry
{
    public required string Id { get; init; }

    public required DateTimeOffset AppliedAtUtc { get; init; }

    public required string Description { get; init; }

    public bool Known { get; init; }
}

public sealed record DatabaseMigrationRunRecord
{
    public required Guid Id { get; init; }

    public required string MigrationId { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? FinishedAtUtc { get; init; }

    public required DatabaseMigrationRunStatus Status { get; init; }

    public string? BackupPath { get; init; }

    public string? Error { get; init; }
}

public sealed record DatabaseMigrationPlan
{
    public required string DatabasePath { get; init; }

    public bool DatabaseExists { get; init; }

    public int UserVersion { get; init; }

    public int SupportedUserVersion { get; init; }

    public DatabaseCompatibilityStatus Status { get; init; }

    public bool IntegrityOk { get; init; }

    public string IntegrityMessage { get; init; } = string.Empty;

    public List<DatabaseMigrationHistoryEntry> AppliedMigrations { get; init; } = [];

    public List<DatabaseMigrationDescriptor> PendingMigrations { get; init; } = [];

    public List<string> UnknownAppliedMigrationIds { get; init; } = [];

    public bool RequiresBackup { get; init; }

    public bool CanApply { get; init; }

    public string Message { get; init; } = string.Empty;
}

public sealed record DatabaseMigrationHistory
{
    public required string DatabasePath { get; init; }

    public List<DatabaseMigrationHistoryEntry> AppliedMigrations { get; init; } = [];

    public List<DatabaseMigrationRunRecord> Runs { get; init; } = [];
}

public sealed record DatabaseMigrationResult
{
    public bool Success { get; init; }

    public bool Changed { get; init; }

    public string? BackupPath { get; init; }

    public List<string> AppliedMigrationIds { get; init; } = [];

    public string? FailedMigrationId { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset FinishedAtUtc { get; init; }

    public string Message { get; init; } = string.Empty;
}

internal sealed record DatabaseMigrationDefinition(
    DatabaseMigrationDescriptor Descriptor,
    string Sql);
