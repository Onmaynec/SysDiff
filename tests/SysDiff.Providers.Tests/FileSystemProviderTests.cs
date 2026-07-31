using SysDiff.Domain;
using SysDiff.Providers;

namespace SysDiff.Providers.Tests;

public sealed class FileSystemProviderTests
{
    [Fact]
    public async Task CaptureAsync_ReturnsCreatedFile()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sysdiff-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "sample.txt");
        await File.WriteAllTextAsync(file, "hello");

        try
        {
            var profile = new CaptureProfile
            {
                Name = "test",
                Providers = new Dictionary<string, ProviderOptions>
                {
                    ["filesystem"] = new()
                    {
                        Roots = [root],
                        HashMode = HashMode.Full,
                        MaximumDepth = 2
                    }
                }
            };

            var provider = new FileSystemProvider();
            ProviderSnapshotResult result = await provider.CaptureAsync(
                new SnapshotContext
                {
                    Profile = profile,
                    DataDirectory = root
                },
                CancellationToken.None);

            Assert.Contains(
                result.Artifacts,
                x => x.Identity.EndsWith("/sample.txt", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
