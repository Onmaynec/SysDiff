using SysDiff.Cli;

namespace SysDiff.Cli.Tests;

public sealed class UpdateInstallerTests
{
    [Fact]
    public void CreatePlan_UsesDedicatedBackupHelperAndLogPaths()
    {
        using var directory = new TemporaryDirectory();
        string staging = Path.Combine(directory.Path, "staging");
        string updates = Path.Combine(directory.Path, "updates");
        string target = Path.Combine(directory.Path, "installed", "sysdiff.exe");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllBytes(Path.Combine(staging, "sysdiff.exe"), [0x4d, 0x5a]);
        var state = new UpdateState
        {
            Status = UpdateStatus.Downloaded,
            StagingDirectory = staging,
            Manifest = CreateManifest()
        };

        UpdateInstallPlan plan = UpdateInstaller.CreatePlan(
            state,
            target,
            updates,
            restartAfterInstall: true);

        Assert.Equal("0.7.1", plan.Version);
        Assert.Equal(Path.GetFullPath(target), plan.TargetExecutable);
        Assert.Equal(Path.GetFullPath(target) + ".backup", plan.BackupExecutable);
        Assert.EndsWith("apply-0.7.1.ps1", plan.HelperScript, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("apply-0.7.1.log", plan.LogPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(plan.RestartAfterInstall);
    }

    [Fact]
    public void HelperScript_WaitsVerifiesAndRollsBack()
    {
        string script = UpdateInstaller.BuildHelperScript();

        Assert.Contains("Wait-Process -Id $ParentProcessId", script, StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $TargetExecutable -Destination $BackupExecutable", script, StringComparison.Ordinal);
        Assert.Contains("& $TargetExecutable --version", script, StringComparison.Ordinal);
        Assert.Contains("Previous executable restored", script, StringComparison.Ordinal);
        Assert.Contains("-LiteralPath", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreatePlan_RejectsMissingStagedExecutable()
    {
        using var directory = new TemporaryDirectory();
        var state = new UpdateState
        {
            Status = UpdateStatus.Downloaded,
            StagingDirectory = Path.Combine(directory.Path, "missing"),
            Manifest = CreateManifest()
        };

        Assert.Throws<FileNotFoundException>(() =>
            UpdateInstaller.CreatePlan(
                state,
                Path.Combine(directory.Path, "sysdiff.exe"),
                Path.Combine(directory.Path, "updates"),
                restartAfterInstall: false));
    }

    private static ReleaseManifest CreateManifest() =>
        new()
        {
            Product = "SysDiff",
            Version = "0.7.1",
            Channel = "stable",
            Runtime = "win-x64",
            Tag = "v0.7.1",
            AssetName = "SysDiff-0.7.1-win-x64.zip",
            AssetUrl =
                "https://github.com/Onmaynec/SysDiff/releases/download/v0.7.1/SysDiff-0.7.1-win-x64.zip",
            Sha256 = new string('a', 64),
            SizeBytes = 100,
            MinimumUpdaterVersion = "0.7.0",
            PublishedAtUtc = DateTimeOffset.UtcNow,
            Unsigned = true
        };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"sysdiff-installer-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
