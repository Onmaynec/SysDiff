namespace SysDiff.Domain;

public enum InvestigationCaseStatus
{
    Open,
    Closed
}

public enum TimelineEventKind
{
    Snapshot,
    Comparison,
    DriftScan,
    Report,
    Case,
    Note
}

public enum DriftLevel
{
    Stable,
    Notice,
    Elevated,
    High,
    Critical
}

public sealed record BaselineRecord
{
    public required Guid SnapshotId { get; init; }

    public required string SnapshotName { get; init; }

    public DateTimeOffset SetAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string? Note { get; init; }
}

public sealed record InvestigationLink
{
    public required string Kind { get; init; }

    public required string ReferenceId { get; init; }

    public required string DisplayName { get; init; }

    public DateTimeOffset LinkedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record InvestigationCaseRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public HashSet<string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public InvestigationCaseStatus Status { get; init; } = InvestigationCaseStatus.Open;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ClosedAtUtc { get; init; }

    public List<InvestigationLink> Links { get; init; } = [];
}

public sealed record TimelineEventRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public TimelineEventKind Kind { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public required string Title { get; init; }

    public string? ReferenceId { get; init; }

    public Guid? CaseId { get; init; }

    public Severity? Severity { get; init; }

    public string Status { get; init; } = "Completed";

    public Dictionary<string, string?> Metadata { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed record DriftRiskSummary
{
    public int Score { get; init; }

    public DriftLevel Level { get; init; }

    public int TotalChanges { get; init; }

    public Dictionary<Severity, int> SeverityCounts { get; init; } = [];

    public List<string> Factors { get; init; } = [];

    public bool PartialData { get; init; }
}

public sealed record DriftScanResult
{
    public required BaselineRecord Baseline { get; init; }

    public required SnapshotRecord CurrentSnapshot { get; init; }

    public required ComparisonResult Comparison { get; init; }

    public required DriftRiskSummary Risk { get; init; }

    public string? HtmlReportPath { get; init; }

    public string? JsonReportPath { get; init; }
}

public interface IInvestigationStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task<BaselineRecord?> GetBaselineAsync(CancellationToken cancellationToken);

    Task SetBaselineAsync(BaselineRecord baseline, CancellationToken cancellationToken);

    Task ClearBaselineAsync(CancellationToken cancellationToken);

    Task<InvestigationCaseRecord> CreateCaseAsync(
        InvestigationCaseRecord investigationCase,
        CancellationToken cancellationToken);

    Task UpdateCaseAsync(
        InvestigationCaseRecord investigationCase,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InvestigationCaseRecord>> ListCasesAsync(
        CancellationToken cancellationToken);

    Task<InvestigationCaseRecord?> GetCaseAsync(
        string nameOrId,
        CancellationToken cancellationToken);

    Task SetActiveCaseAsync(Guid? caseId, CancellationToken cancellationToken);

    Task<InvestigationCaseRecord?> GetActiveCaseAsync(CancellationToken cancellationToken);

    Task LinkAsync(
        Guid caseId,
        InvestigationLink link,
        CancellationToken cancellationToken);

    Task AppendTimelineAsync(
        TimelineEventRecord timelineEvent,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TimelineEventRecord>> ListTimelineAsync(
        int limit,
        TimelineEventKind? kind,
        CancellationToken cancellationToken);
}
