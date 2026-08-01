using SysDiff.Core;
using SysDiff.Domain;

namespace SysDiff.Core.Tests;

public sealed class ComparisonMoveTests
{
    private readonly ComparisonEngine _engine =
        new(new SeverityEngine(), new NoiseFilterEngine());

    [Fact]
    public void Compare_DetectsRenamedFileByUniqueHashAndSize()
    {
        SnapshotRecord before = Snapshot(
            "before",
            File(@"C:\Demo\old.exe", "abc", 42));
        SnapshotRecord after = Snapshot(
            "after",
            File(@"C:\Demo\new.exe", "abc", 42));

        ComparisonResult result = _engine.Compare(before, after, NoiseMode.Raw);

        SystemChange change = Assert.Single(result.Changes);
        Assert.Equal(ChangeType.Renamed, change.ChangeType);
        Assert.Equal(0.95, change.Confidence, precision: 2);
    }

    [Fact]
    public void Compare_DoesNotMergeAmbiguousDuplicateHashes()
    {
        SnapshotRecord before = Snapshot(
            "before",
            File(@"C:\A\one.bin", "same", 10),
            File(@"C:\A\two.bin", "same", 10));
        SnapshotRecord after = Snapshot(
            "after",
            File(@"C:\B\three.bin", "same", 10));

        ComparisonResult result = _engine.Compare(before, after, NoiseMode.Raw);

        Assert.Equal(3, result.Changes.Count);
        Assert.DoesNotContain(result.Changes, x =>
            x.ChangeType is ChangeType.Moved or ChangeType.Renamed);
    }

    [Fact]
    public void Compare_CrossMachineAddsWarningAndLowersConfidence()
    {
        SnapshotRecord before = Snapshot("before") with { MachineFingerprint = "a" };
        SnapshotRecord after = Snapshot(
            "after",
            File(@"C:\Demo\new.exe", "abc", 42)) with
        {
            MachineFingerprint = "b"
        };

        ComparisonResult result = _engine.Compare(
            before,
            after,
            NoiseMode.Raw,
            crossMachine: true);

        Assert.True(result.CrossMachine);
        Assert.NotEmpty(result.Warnings);
        Assert.Equal(0.75, Assert.Single(result.Changes).Confidence, precision: 2);
    }

    private static SnapshotRecord Snapshot(string name, params SystemArtifact[] artifacts) =>
        new()
        {
            Name = name,
            Status = SnapshotStatus.Completed,
            MachineFingerprint = "same",
            Artifacts = [.. artifacts]
        };

    private static SystemArtifact File(string path, string hash, long size) => new()
    {
        ProviderId = "filesystem",
        ArtifactType = "File",
        Identity = $"file://{path.Replace('\\', '/')}",
        DisplayName = path,
        Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["Path"] = ArtifactValue.From(path),
            ["Sha256"] = ArtifactValue.From(hash),
            ["Size"] = ArtifactValue.From(size, "Int64")
        }
    };
}
