namespace SysDiff.Cli;

public static class ProductInfo
{
    public const string Name = "SysDiff";
    public const string Version = "0.9.0";
    public const string Runtime = "win-x64";
    public const string Channel = "stable";
    public const string Repository = "Onmaynec/SysDiff";
    public const string StableManifestUrl =
        "https://github.com/Onmaynec/SysDiff/releases/latest/download/release-manifest.json";

    public static ReleaseVersion ParsedVersion => ReleaseVersion.Parse(Version);
}
