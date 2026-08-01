using SysDiff.Cli;

namespace SysDiff.Cli.Tests;

public sealed class CyberConsoleTests
{
    [Theory]
    [InlineData(ConsoleKey.D1, 0)]
    [InlineData(ConsoleKey.NumPad4, 3)]
    [InlineData(ConsoleKey.D9, 8)]
    [InlineData(ConsoleKey.P, 0)]
    [InlineData(ConsoleKey.C, 1)]
    [InlineData(ConsoleKey.W, 2)]
    [InlineData(ConsoleKey.L, 3)]
    [InlineData(ConsoleKey.D, 5)]
    public void CommandDeck_ResolvesDashboardHotkeys(ConsoleKey key, int expected)
    {
        Assert.Equal(expected, TerminalMenuNavigator.ResolveDirectIndex(key, itemCount: 9));
    }

    [Fact]
    public void DirectHotkey_SelectsAndActivatesModule()
    {
        var navigator = new TerminalMenuNavigator(itemCount: 9);

        TerminalNavigationAction action = navigator.Apply(ConsoleKey.D4);

        Assert.Equal(TerminalNavigationAction.Activate, action);
        Assert.Equal(3, navigator.SelectedIndex);
    }

    [Theory]
    [InlineData(CyberStageState.Queued, "[--]")]
    [InlineData(CyberStageState.Running, "[>>]")]
    [InlineData(CyberStageState.Completed, "[OK]")]
    [InlineData(CyberStageState.Warning, "[!!]")]
    [InlineData(CyberStageState.Failed, "[XX]")]
    [InlineData(CyberStageState.Cancelled, "[//]")]
    public void StageMarkers_AreReadableWithoutColors(
        CyberStageState state,
        string expected)
    {
        Assert.Equal(expected, CyberTheme.StageMarker(state));
    }

    [Fact]
    public void ProgressBar_HasStableRequestedWidth()
    {
        string bar = CyberAnimation.BuildProgressBar(0.5, width: 20, tick: 7);

        Assert.Equal(20, bar.Length);
        Assert.Contains('█', bar);
        Assert.Contains('░', bar);
    }

    [Fact]
    public void Scanner_HasSingleActiveHead()
    {
        string scanner = CyberAnimation.BuildScanner(tick: 5, width: 24);

        Assert.Equal(24, scanner.Length);
        Assert.Equal(1, scanner.Count(character => character == '█'));
    }

    [Theory]
    [InlineData(true, false, null, null, true)]
    [InlineData(true, false, "1", null, false)]
    [InlineData(true, false, null, "true", false)]
    [InlineData(false, false, null, null, false)]
    [InlineData(true, true, null, null, false)]
    public void MotionPolicy_EnablesOnlySafeInteractiveAnimations(
        bool interactive,
        bool outputRedirected,
        string? disabled,
        string? ci,
        bool expected)
    {
        bool actual = TerminalMotionPolicy.ShouldAnimate(
            interactive,
            outputRedirected,
            disabled,
            ci);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("YES", true)]
    [InlineData("off", false)]
    [InlineData(null, false)]
    public void TruthyEnvironmentValues_AreParsed(string? value, bool expected)
    {
        Assert.Equal(expected, TerminalMotionPolicy.IsTruthy(value));
    }
}
