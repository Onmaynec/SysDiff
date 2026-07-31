namespace SysDiff.Domain;

public interface ISnapshotProvider
{
    string Id { get; }

    string DisplayName { get; }

    bool RequiresAdministrator { get; }

    Task<ProviderSnapshotResult> CaptureAsync(
        SnapshotContext context,
        CancellationToken cancellationToken);
}

public interface ISnapshotStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task SaveSnapshotAsync(SnapshotRecord snapshot, CancellationToken cancellationToken);

    Task<IReadOnlyList<SnapshotRecord>> ListSnapshotsAsync(CancellationToken cancellationToken);

    Task<SnapshotRecord?> GetSnapshotAsync(string nameOrId, CancellationToken cancellationToken);

    Task DeleteSnapshotAsync(string nameOrId, CancellationToken cancellationToken);

    Task SaveComparisonAsync(ComparisonResult comparison, CancellationToken cancellationToken);

    Task<ComparisonResult?> GetComparisonAsync(Guid id, CancellationToken cancellationToken);
}

public interface ISeverityEngine
{
    (Severity Severity, string Explanation, string WhyThisMatters) Evaluate(
        ChangeType changeType,
        SystemArtifact? before,
        SystemArtifact? after,
        IReadOnlyCollection<PropertyChange> changedProperties);
}

public interface INoiseFilterEngine
{
    IReadOnlyList<SystemChange> Apply(
        IEnumerable<SystemChange> changes,
        NoiseMode mode,
        out int hiddenCount);
}
