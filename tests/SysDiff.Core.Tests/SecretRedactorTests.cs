using SysDiff.Core;

namespace SysDiff.Core.Tests;

public sealed class SecretRedactorTests
{
    [Theory]
    [InlineData("Password")]
    [InlineData("api_token")]
    [InlineData("PrivateKey")]
    public void Protect_RedactsSensitiveNames(string name)
    {
        var value = SecretRedactor.Protect(name, "super-secret", "String");

        Assert.True(value.Redacted);
        Assert.Equal("<redacted>", value.Value);
        Assert.False(string.IsNullOrWhiteSpace(value.Hash));
    }

    [Fact]
    public void Protect_KeepsOrdinaryValue()
    {
        var value = SecretRedactor.Protect("InstallPath", @"C:\Tools", "String");

        Assert.False(value.Redacted);
        Assert.Equal(@"C:\Tools", value.Value);
    }
}
