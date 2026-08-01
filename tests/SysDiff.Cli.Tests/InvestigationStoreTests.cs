using SysDiff.Domain;
using SysDiff.Storage;

namespace SysDiff.Cli.Tests;

public sealed class InvestigationStoreTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "sysdiff-tests",
        Guid.NewGuid().ToString("N"));

    private string DatabasePath => Path.Combine(_directory, "sysdiff.db");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Initialize_IsIdempotent_AndBaselineRoundTrips()
    {
        var snapshots = new SqliteSnapshotStore(DatabasePath);
        var investigations = new SqliteInvestigationStore(DatabasePath);
        await snapshots.InitializeAsync(CancellationToken.None);
        await investigations.InitializeAsync(CancellationToken.None);
        await investigations.InitializeAsync(CancellationToken.None);

        var snapshot = new SnapshotRecord
        {
            Name = "trusted-baseline",
            Status = SnapshotStatus.Completed
        };
        await snapshots.SaveSnapshotAsync(snapshot, CancellationToken.None);
        await investigations.SetBaselineAsync(new BaselineRecord
        {
            SnapshotId = snapshot.Id,
            SnapshotName = snapshot.Name,
            Note = "clean state"
        }, CancellationToken.None);

        BaselineRecord? actual = await investigations.GetBaselineAsync(CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(snapshot.Id, actual.SnapshotId);
        Assert.Equal(snapshot.Name, actual.SnapshotName);
        Assert.Equal("clean state", actual.Note);
    }

    [Fact]
    public async Task CaseLinksAndTimeline_ArePersisted()
    {
        var snapshots = new SqliteSnapshotStore(DatabasePath);
        var investigations = new SqliteInvestigationStore(DatabasePath);
        await snapshots.InitializeAsync(CancellationToken.None);
        await investigations.InitializeAsync(CancellationToken.None);

        InvestigationCaseRecord created = await investigations.CreateCaseAsync(
            new InvestigationCaseRecord
            {
                Name = "Installer audit",
                Description = "Test case",
                Tags = new HashSet<string>(["installer", "test"], StringComparer.OrdinalIgnoreCase)
            },
            CancellationToken.None);
        await investigations.SetActiveCaseAsync(created.Id, CancellationToken.None);
        await investigations.LinkAsync(created.Id, new InvestigationLink
        {
            Kind = "report",
            ReferenceId = "report.html",
            DisplayName = "report.html"
        }, CancellationToken.None);
        await investigations.AppendTimelineAsync(new TimelineEventRecord
        {
            Kind = TimelineEventKind.DriftScan,
            Title = "Drift Scan test",
            ReferenceId = Guid.NewGuid().ToString("D"),
            CaseId = created.Id,
            Severity = Severity.High,
            Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["score"] = "55"
            }
        }, CancellationToken.None);

        InvestigationCaseRecord? active = await investigations.GetActiveCaseAsync(CancellationToken.None);
        InvestigationCaseRecord? loaded = await investigations.GetCaseAsync(created.Id.ToString("D"), CancellationToken.None);
        IReadOnlyList<TimelineEventRecord> timeline = await investigations.ListTimelineAsync(
            10,
            TimelineEventKind.DriftScan,
            CancellationToken.None);

        Assert.NotNull(active);
        Assert.Equal(created.Id, active.Id);
        Assert.NotNull(loaded);
        Assert.Single(loaded.Links);
        Assert.Single(timeline);
        Assert.Equal("55", timeline[0].Metadata["score"]);
    }

    [Fact]
    public async Task MissingBaselineSnapshot_ReturnsNull()
    {
        var snapshots = new SqliteSnapshotStore(DatabasePath);
        var investigations = new SqliteInvestigationStore(DatabasePath);
        await snapshots.InitializeAsync(CancellationToken.None);
        await investigations.InitializeAsync(CancellationToken.None);

        var snapshot = new SnapshotRecord
        {
            Name = "temporary",
            Status = SnapshotStatus.Completed
        };
        await snapshots.SaveSnapshotAsync(snapshot, CancellationToken.None);
        await investigations.SetBaselineAsync(new BaselineRecord
        {
            SnapshotId = snapshot.Id,
            SnapshotName = snapshot.Name
        }, CancellationToken.None);
        await snapshots.DeleteSnapshotAsync(snapshot.Id.ToString("D"), CancellationToken.None);

        BaselineRecord? baseline = await investigations.GetBaselineAsync(CancellationToken.None);

        Assert.Null(baseline);
    }
}
