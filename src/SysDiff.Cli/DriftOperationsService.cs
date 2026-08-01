using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using SysDiff.Core;
using SysDiff.Domain;
using SysDiff.Reporting;

namespace SysDiff.Cli;

internal sealed class DriftOperationsService
{
    private readonly AppPaths _paths;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IInvestigationStore _investigationStore;
    private readonly SnapshotCoordinator _coordinator;
    private readonly ComparisonEngine _comparisonEngine;
    private readonly DriftRiskEngine _riskEngine;
    private readonly HtmlReportRenderer _htmlRenderer;

    public DriftOperationsService(
        AppPaths paths,
        ISnapshotStore snapshotStore,
        IInvestigationStore investigationStore,
        SnapshotCoordinator coordinator,
        ComparisonEngine comparisonEngine,
        DriftRiskEngine riskEngine,
        HtmlReportRenderer htmlRenderer)
    {
        _paths = paths;
        _snapshotStore = snapshotStore;
        _investigationStore = investigationStore;
        _coordinator = coordinator;
        _comparisonEngine = comparisonEngine;
        _riskEngine = riskEngine;
        _htmlRenderer = htmlRenderer;
    }

    public async Task<BaselineRecord> SetBaselineAsync(
        string nameOrId,
        string? note,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameOrId);
        SnapshotRecord snapshot = await _snapshotStore.GetSnapshotAsync(nameOrId, cancellationToken)
            ?? throw new InvalidOperationException("Снимок для baseline не найден.");
        if (snapshot.Status is SnapshotStatus.Failed or SnapshotStatus.Cancelled or SnapshotStatus.Corrupted)
        {
            throw new InvalidOperationException(
                $"Снимок со статусом {snapshot.Status} нельзя использовать как baseline.");
        }

        var baseline = new BaselineRecord
        {
            SnapshotId = snapshot.Id,
            SnapshotName = snapshot.Name,
            SetAtUtc = DateTimeOffset.UtcNow,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };
        await _investigationStore.SetBaselineAsync(baseline, cancellationToken);
        await _investigationStore.AppendTimelineAsync(new TimelineEventRecord
        {
            Kind = TimelineEventKind.Note,
            TimestampUtc = baseline.SetAtUtc,
            Title = $"Baseline установлена: {snapshot.Name}",
            ReferenceId = snapshot.Id.ToString("D"),
            Status = "Pinned",
            Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["note"] = baseline.Note
            }
        }, cancellationToken);
        await LinkActiveCaseAsync(
            new InvestigationLink
            {
                Kind = "baseline",
                ReferenceId = snapshot.Id.ToString("D"),
                DisplayName = snapshot.Name
            },
            cancellationToken);
        return baseline;
    }

    public async Task ClearBaselineAsync(CancellationToken cancellationToken)
    {
        BaselineRecord? current = await _investigationStore.GetBaselineAsync(cancellationToken);
        await _investigationStore.ClearBaselineAsync(cancellationToken);
        await _investigationStore.AppendTimelineAsync(new TimelineEventRecord
        {
            Kind = TimelineEventKind.Note,
            Title = current is null
                ? "Baseline очищена"
                : $"Baseline снята: {current.SnapshotName}",
            ReferenceId = current?.SnapshotId.ToString("D"),
            Status = "Cleared"
        }, cancellationToken);
    }

    public async Task<DriftScanResult> ScanAsync(
        CaptureProfile profile,
        NoiseMode noiseMode,
        IProgress<SnapshotProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        BaselineRecord baseline = await _investigationStore.GetBaselineAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Baseline не настроена. Сначала выполните baseline set <snapshot>.");
        SnapshotRecord before = await _snapshotStore.GetSnapshotAsync(
            baseline.SnapshotId.ToString("D"),
            cancellationToken)
            ?? throw new InvalidOperationException("Снимок активной baseline больше не существует.");

        string stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
        SnapshotRecord current = await _coordinator.CaptureAsync(
            $"drift-{stamp}",
            profile,
            _paths.DataDirectory,
            IsAdministrator(),
            progress,
            cancellationToken);

        ComparisonResult comparison = _comparisonEngine.Compare(before, current, noiseMode);
        await _snapshotStore.SaveComparisonAsync(comparison, cancellationToken);
        bool partialData = before.Status == SnapshotStatus.Partial
            || current.Status == SnapshotStatus.Partial;
        DriftRiskSummary risk = _riskEngine.Evaluate(comparison, partialData);

        Directory.CreateDirectory(_paths.ReportsDirectory);
        string prefix = Path.Combine(_paths.ReportsDirectory, $"drift-{stamp}");
        string htmlPath = prefix + ".html";
        string jsonPath = prefix + ".json";
        await File.WriteAllTextAsync(
            htmlPath,
            _htmlRenderer.Render(before, current, comparison),
            cancellationToken);
        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(
                new
                {
                    generatedAtUtc = DateTimeOffset.UtcNow,
                    baseline = new { baseline.SnapshotId, baseline.SnapshotName, baseline.SetAtUtc },
                    current = new { current.Id, current.Name, current.Status, current.ProfileName },
                    comparisonId = comparison.Id,
                    noiseMode = comparison.NoiseMode,
                    risk
                },
                JsonOptions),
            cancellationToken);

        InvestigationCaseRecord? activeCase =
            await _investigationStore.GetActiveCaseAsync(cancellationToken);
        await _investigationStore.AppendTimelineAsync(new TimelineEventRecord
        {
            Kind = TimelineEventKind.DriftScan,
            TimestampUtc = comparison.CreatedAtUtc,
            Title = $"Drift Scan: {baseline.SnapshotName} → {current.Name}",
            ReferenceId = comparison.Id.ToString("D"),
            CaseId = activeCase?.Id,
            Severity = ToSeverity(risk.Level),
            Status = risk.Level.ToString(),
            Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["score"] = risk.Score.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["changes"] = risk.TotalChanges.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["snapshotId"] = current.Id.ToString("D"),
                ["htmlReport"] = htmlPath,
                ["jsonReport"] = jsonPath
            }
        }, cancellationToken);

        if (activeCase is not null)
        {
            await _investigationStore.LinkAsync(activeCase.Id, new InvestigationLink
            {
                Kind = "snapshot",
                ReferenceId = current.Id.ToString("D"),
                DisplayName = current.Name
            }, cancellationToken);
            await _investigationStore.LinkAsync(activeCase.Id, new InvestigationLink
            {
                Kind = "comparison",
                ReferenceId = comparison.Id.ToString("D"),
                DisplayName = $"{baseline.SnapshotName} → {current.Name}"
            }, cancellationToken);
            await _investigationStore.LinkAsync(activeCase.Id, new InvestigationLink
            {
                Kind = "report",
                ReferenceId = htmlPath,
                DisplayName = Path.GetFileName(htmlPath)
            }, cancellationToken);
        }

        return new DriftScanResult
        {
            Baseline = baseline,
            CurrentSnapshot = current,
            Comparison = comparison,
            Risk = risk,
            HtmlReportPath = htmlPath,
            JsonReportPath = jsonPath
        };
    }

    private async Task LinkActiveCaseAsync(
        InvestigationLink link,
        CancellationToken cancellationToken)
    {
        InvestigationCaseRecord? activeCase =
            await _investigationStore.GetActiveCaseAsync(cancellationToken);
        if (activeCase is not null)
        {
            await _investigationStore.LinkAsync(activeCase.Id, link, cancellationToken);
        }
    }

    private static Severity ToSeverity(DriftLevel level) => level switch
    {
        DriftLevel.Critical => Severity.Critical,
        DriftLevel.High => Severity.High,
        DriftLevel.Elevated => Severity.Medium,
        DriftLevel.Notice => Severity.Low,
        _ => Severity.Info
    };

    private static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
