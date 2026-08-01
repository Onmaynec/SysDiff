using SysDiff.Cli;

namespace SysDiff.Cli.Tests;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("0.7.0", "0.6.9", 1)]
    [InlineData("1.0.0", "1.0.0-rc.1", 1)]
    [InlineData("1.0.0-rc.2", "1.0.0-rc.1", 1)]
    [InlineData("1.0.0-beta.11", "1.0.0-beta.2", 1)]
    [InlineData("v0.7.0", "0.7.0+build.5", 0)]
    public void CompareTo_FollowsSemanticVersioning(
        string left,
        string right,
        int expectedSign)
    {
        int actual = Math.Sign(
            ReleaseVersion.Parse(left).CompareTo(ReleaseVersion.Parse(right)));

        Assert.Equal(expectedSign, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("01.0.0")]
    [InlineData("1.01.0")]
    [InlineData("1.0.01")]
    [InlineData("1.0.0-01")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0+bad metadata")]
    public void TryParse_RejectsInvalidVersions(string value)
    {
        Assert.False(ReleaseVersion.TryParse(value, out _));
    }

    [Fact]
    public void ToString_NormalizesVPrefixAndBuildMetadata()
    {
        ReleaseVersion version = ReleaseVersion.Parse("v2.4.1+windows.7");

        Assert.Equal("2.4.1", version.ToString());
    }
}
