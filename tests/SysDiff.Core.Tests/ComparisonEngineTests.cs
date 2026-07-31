using SysDiff.Core;
using SysDiff.Domain;

namespace SysDiff.Core.Tests;

public sealed class ComparisonEngineTests
{
    private readonly ComparisonEngine _engine =
        new(new SeverityEngine(), new NoiseFilterEngine());

    [Fact]
    public void Compare_DetectsAddedArtifact()
    {
        SnapshotRecord before = Snapshot("before");
        SnapshotRecord after = Snapshot("after", Artifact("service://demo", "services", ("Status", "Running")));

        ComparisonResult result = _engine.Compare(before, after, NoiseMode.Raw);

        SystemChange change = Assert.Single(result.Changes);
        Assert.Equal(ChangeType.Added, change.ChangeType);
        Assert.Equal("service://demo", change.Identity);
    }

    [Fact]
    public void Compare_DetectsRemovedArtifact()
    {
        SnapshotRecord before = Snapshot("before", Artifact("file://c:/demo.txt", "filesystem", ("Size", "10")));
        SnapshotRecord after = Snapshot("after");

        ComparisonResult result = _engine.Compare(before, after, NoiseMode.Raw);

        Assert.Equal(ChangeType.Removed, Assert.Single(result.Changes).ChangeType);
    }

    [Fact]
    public void Compare_DetectsModifiedProperty()
    {
        SnapshotRecord before = Snapshot(
            "before",
            Artifact("environment://user/path/c:/tools", "environment", ("Order", "1")));
        SnapshotRecord after = Snapshot(
            "after",
            Artifact("environment://user/path/c:/tools", "environment", ("Order", "2")));

        ComparisonResult result = _engine.Compare(before, after, NoiseMode.Raw);

        SystemChange change = Assert.Single(result.Changes);
        Assert.Equal(ChangeType.Modified, change.ChangeType);
        Assert.Equal("Order", Assert.Single(change.ChangedProperties).Name);
    }

    [Fact]
    public void Compare_IgnoresDictionaryInsertionOrder()
    {
        SystemArtifact first = Artifact(
            "service://demo",
            "services",
            ("Name", "demo"),
            ("Status", "Running"));
        SystemArtifact second = Artifact(
            "service://demo",
            "services",
            ("Status", "Running"),
            ("Name", "demo"));

        ComparisonResult result = _engine.Compare(
            Snapshot("before", first),
            Snapshot("after", second),
            NoiseMode.Raw);

        Assert.Empty(result.Changes);
    }

    private static SnapshotRecord Snapshot(string name, params SystemArtifact[] artifacts) =>
        new()
        {
            Name = name,
            Status = SnapshotStatus.Completed,
            Artifacts = [.. artifacts]
        };

    private static SystemArtifact Artifact(
        string identity,
        string provider,
        params (string Name, string Value)[] values) =>
        new()
        {
            ProviderId = provider,
            ArtifactType = "Test",
            Identity = identity,
            DisplayName = identity,
            Properties = values.ToDictionary(
                x => x.Name,
                x => ArtifactValue.From(x.Value),
                StringComparer.OrdinalIgnoreCase)
        };
}
