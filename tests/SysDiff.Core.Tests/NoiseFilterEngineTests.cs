using SysDiff.Core;
using SysDiff.Domain;

namespace SysDiff.Core.Tests;

public sealed class NoiseFilterEngineTests
{
    [Fact]
    public void Balanced_HidesLowSeverityTemporaryFile()
    {
        var engine = new NoiseFilterEngine();
        var change = new SystemChange
        {
            ChangeType = ChangeType.Modified,
            ProviderId = "filesystem",
            ArtifactType = "File",
            Identity = "file://c:/users/demo/appdata/local/temp/a.log",
            DisplayName = "a.log",
            Severity = Severity.Low,
            After = new SystemArtifact
            {
                ProviderId = "filesystem",
                ArtifactType = "File",
                Identity = "file://c:/users/demo/appdata/local/temp/a.log",
                DisplayName = "a.log",
                Properties = new Dictionary<string, ArtifactValue>
                {
                    ["Path"] = ArtifactValue.From(@"C:\Users\Demo\AppData\Local\Temp\a.log")
                }
            }
        };

        IReadOnlyList<SystemChange> visible =
            engine.Apply([change], NoiseMode.Balanced, out int hidden);

        Assert.Empty(visible);
        Assert.Equal(1, hidden);
    }

    [Fact]
    public void Raw_KeepsTemporaryFile()
    {
        var engine = new NoiseFilterEngine();
        var change = new SystemChange
        {
            ChangeType = ChangeType.Added,
            ProviderId = "filesystem",
            ArtifactType = "File",
            Identity = "file://c:/temp/a.tmp",
            DisplayName = "a.tmp",
            Severity = Severity.Low
        };

        IReadOnlyList<SystemChange> visible =
            engine.Apply([change], NoiseMode.Raw, out int hidden);

        Assert.Single(visible);
        Assert.Equal(0, hidden);
    }
}
