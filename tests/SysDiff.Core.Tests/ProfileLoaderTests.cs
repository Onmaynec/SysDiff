using SysDiff.Core;

namespace SysDiff.Core.Tests;

public sealed class ProfileLoaderTests
{
    [Fact]
    public async Task LoadAsync_ReadsValidProfile()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(
                path,
                """
                {
                  "name": "custom",
                  "description": "test",
                  "providers": {
                    "filesystem": {
                      "enabled": true,
                      "roots": ["C:\\\\Demo"],
                      "maximumDepth": 4,
                      "maximumArtifacts": 100
                    }
                  }
                }
                """);
            var loader = new ProfileLoader();

            SysDiff.Domain.CaptureProfile profile = await loader.LoadAsync(
                path,
                ["filesystem"],
                CancellationToken.None);

            Assert.Equal("custom", profile.Name);
            Assert.Equal(4, profile.Providers["filesystem"].MaximumDepth);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_RejectsUnknownProvider()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(
                path,
                """
                {
                  "name": "custom",
                  "providers": {
                    "unknown-provider": { "enabled": true }
                  }
                }
                """);
            var loader = new ProfileLoader();

            await Assert.ThrowsAsync<InvalidDataException>(() => loader.LoadAsync(
                path,
                ["filesystem"],
                CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
