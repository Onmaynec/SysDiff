using SysDiff.Cli;

namespace SysDiff.Cli.Tests;

public sealed class UpdateSettingsStoreTests
{
    [Fact]
    public async Task Settings_RoundTripPreservesSafeValues()
    {
        using var directory = new TemporaryDirectory();
        var paths = CreatePaths(directory.Path);
        var store = new UpdateSettingsStore(paths);
        var expected = new UpdateSettings
        {
            AutoCheck = false,
            AutoDownload = true,
            CheckIntervalHours = 48,
            IgnoredVersion = "0.8.0"
        };

        await store.SaveSettingsAsync(expected, CancellationToken.None);
        UpdateSettings actual = await store.LoadSettingsAsync(CancellationToken.None);

        Assert.False(actual.AutoCheck);
        Assert.True(actual.AutoDownload);
        Assert.Equal(48, actual.CheckIntervalHours);
        Assert.Equal("stable", actual.Channel);
        Assert.Equal("0.8.0", actual.IgnoredVersion);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(24, 24)]
    [InlineData(999, 168)]
    public void Normalize_ClampsCheckInterval(int input, int expected)
    {
        UpdateSettings actual = UpdateSettingsStore.Normalize(
            new UpdateSettings { CheckIntervalHours = input });

        Assert.Equal(expected, actual.CheckIntervalHours);
    }

    [Fact]
    public void ShouldCheck_UsesConfiguredInterval()
    {
        DateTimeOffset now = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
        var due = new UpdateSettings
        {
            AutoCheck = true,
            CheckIntervalHours = 24,
            LastCheckedAtUtc = now.AddHours(-24)
        };
        var notDue = due with { LastCheckedAtUtc = now.AddHours(-23) };

        Assert.True(UpdateSettingsStore.ShouldCheck(due, now));
        Assert.False(UpdateSettingsStore.ShouldCheck(notDue, now));
        Assert.False(UpdateSettingsStore.ShouldCheck(due with { AutoCheck = false }, now));
    }

    [Fact]
    public async Task CorruptSettings_AreQuarantinedAndDefaultsReturned()
    {
        using var directory = new TemporaryDirectory();
        AppPaths paths = CreatePaths(directory.Path);
        Directory.CreateDirectory(paths.DataDirectory);
        await File.WriteAllTextAsync(paths.UpdateSettingsPath, "{broken");
        var store = new UpdateSettingsStore(paths);

        UpdateSettings settings = await store.LoadSettingsAsync(CancellationToken.None);

        Assert.True(settings.AutoCheck);
        Assert.False(settings.AutoDownload);
        Assert.Single(Directory.EnumerateFiles(paths.DataDirectory, "update-settings.json.corrupt-*"));
    }

    private static AppPaths CreatePaths(string root) =>
        new(
            root,
            Path.Combine(root, "data"),
            Path.Combine(root, "data", "sysdiff.db"),
            Path.Combine(root, "reports"),
            Path.Combine(root, "logs"),
            Portable: true);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"sysdiff-update-tests-{Guid.NewGuid():N}");
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
