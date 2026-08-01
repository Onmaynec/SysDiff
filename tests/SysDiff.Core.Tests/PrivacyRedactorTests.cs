using SysDiff.Core;

namespace SysDiff.Core.Tests;

public sealed class PrivacyRedactorTests
{
    [Fact]
    public void Redact_ReplacesArbitraryWindowsUserProfile()
    {
        var redactor = new PrivacyRedactor();

        string result = redactor.Redact(@"C:\Users\Alice\AppData\Local\Demo\app.exe");

        Assert.Equal(@"%USERPROFILE%\AppData\Local\Demo\app.exe", result);
        Assert.DoesNotContain("Alice", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedactArtifact_RedactsIdentityAndProperties()
    {
        var redactor = new PrivacyRedactor();
        var artifact = new SysDiff.Domain.SystemArtifact
        {
            ProviderId = "filesystem",
            ArtifactType = "File",
            Identity = "file://C:/Users/Bob/demo.exe",
            DisplayName = @"C:\Users\Bob\demo.exe",
            Properties = new Dictionary<string, SysDiff.Domain.ArtifactValue>
            {
                ["Path"] = SysDiff.Domain.ArtifactValue.From(@"C:\Users\Bob\demo.exe")
            }
        };

        SysDiff.Domain.SystemArtifact result = redactor.RedactArtifact(artifact);

        Assert.DoesNotContain("Bob", result.Identity, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "%USERPROFILE%",
            result.Properties["Path"].Value ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }
}
