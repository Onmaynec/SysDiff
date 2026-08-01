namespace SysDiff.ProviderSdk;

public static class ProviderSdkInfo
{
    public const string CurrentVersion = "0.3";
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class SysDiffProviderPluginAttribute : Attribute
{
    public SysDiffProviderPluginAttribute(string sdkVersion)
    {
        SdkVersion = sdkVersion;
    }

    public string SdkVersion { get; }

    public string? DisplayName { get; set; }
}
