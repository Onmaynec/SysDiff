using System.Text.Json;
using SysDiff.Cli;

namespace SysDiff.Cli.Tests;

public sealed class ReleaseManifestTests
{
    [Fact]
    public void Parse_AcceptsOfficialStableManifest()
    {
        ReleaseManifest manifest = ReleaseManifest.Parse(CreateJson());

        Assert.Equal("0.7.0", manifest.Version);
        Assert.Equal("v0.7.0", manifest.Tag);
        Assert.Equal(ProductInfo.Runtime, manifest.Runtime);
        Assert.True(manifest.Unsigned);
    }

    [Theory]
    [InlineData("assetUrl", "https://example.com/SysDiff-0.7.0-win-x64.zip")]
    [InlineData("runtime", "linux-x64")]
    [InlineData("channel", "beta")]
    [InlineData("tag", "v0.7.1")]
    [InlineData("assetName", "SysDiff-0.7.1-win-x64.zip")]
    [InlineData("sha256", "bad")]
    public void Parse_RejectsTamperedManifest(string property, object value)
    {
        Dictionary<string, object?> manifest = CreateDictionary();
        manifest[property] = value;
        string json = JsonSerializer.Serialize(manifest);

        Assert.Throws<ReleaseManifestException>(() => ReleaseManifest.Parse(json));
    }

    [Fact]
    public void Parse_RejectsPrereleaseVersionOnStableChannel()
    {
        Dictionary<string, object?> manifest = CreateDictionary();
        manifest["version"] = "0.8.0-rc.1";
        manifest["tag"] = "v0.8.0-rc.1";
        manifest["assetName"] = "SysDiff-0.8.0-rc.1-win-x64.zip";
        manifest["assetUrl"] =
            "https://github.com/Onmaynec/SysDiff/releases/download/v0.8.0-rc.1/SysDiff-0.8.0-rc.1-win-x64.zip";

        Assert.Throws<ReleaseManifestException>(() =>
            ReleaseManifest.Parse(JsonSerializer.Serialize(manifest)));
    }

    private static string CreateJson() => JsonSerializer.Serialize(CreateDictionary());

    private static Dictionary<string, object?> CreateDictionary() =>
        new(StringComparer.Ordinal)
        {
            ["schemaVersion"] = 1,
            ["product"] = "SysDiff",
            ["version"] = "0.7.0",
            ["channel"] = "stable",
            ["runtime"] = "win-x64",
            ["tag"] = "v0.7.0",
            ["assetName"] = "SysDiff-0.7.0-win-x64.zip",
            ["assetUrl"] =
                "https://github.com/Onmaynec/SysDiff/releases/download/v0.7.0/SysDiff-0.7.0-win-x64.zip",
            ["sha256"] = new string('a', 64),
            ["sizeBytes"] = 123456L,
            ["minimumUpdaterVersion"] = "0.7.0",
            ["publishedAtUtc"] = "2026-08-01T10:00:00Z",
            ["unsigned"] = true
        };
}
