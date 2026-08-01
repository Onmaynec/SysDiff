using SysDiff.Cli;

namespace SysDiff.Cli.Tests;

public sealed class TerminalNavigationTests
{
    [Fact]
    public void UpArrow_WrapsFromFirstToLast()
    {
        var navigator = new TerminalMenuNavigator(itemCount: 5);

        TerminalNavigationAction action = navigator.Apply(ConsoleKey.UpArrow);

        Assert.Equal(TerminalNavigationAction.None, action);
        Assert.Equal(4, navigator.SelectedIndex);
    }

    [Fact]
    public void DownArrow_WrapsFromLastToFirst()
    {
        var navigator = new TerminalMenuNavigator(itemCount: 3, selectedIndex: 2);

        navigator.Apply(ConsoleKey.DownArrow);

        Assert.Equal(0, navigator.SelectedIndex);
    }

    [Theory]
    [InlineData(ConsoleKey.Enter, TerminalNavigationAction.Activate)]
    [InlineData(ConsoleKey.Escape, TerminalNavigationAction.Back)]
    [InlineData(ConsoleKey.Q, TerminalNavigationAction.Exit)]
    [InlineData(ConsoleKey.F, TerminalNavigationAction.Filter)]
    [InlineData(ConsoleKey.S, TerminalNavigationAction.Sort)]
    [InlineData(ConsoleKey.R, TerminalNavigationAction.ToggleRaw)]
    [InlineData(ConsoleKey.E, TerminalNavigationAction.Export)]
    [InlineData(ConsoleKey.F5, TerminalNavigationAction.Refresh)]
    public void Hotkeys_ReturnExpectedAction(
        ConsoleKey key,
        TerminalNavigationAction expected)
    {
        var navigator = new TerminalMenuNavigator(itemCount: 2);

        TerminalNavigationAction actual = navigator.Apply(key);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, false)]
    public void InteractiveCapability_RejectsRedirection(
        bool inputRedirected,
        bool outputRedirected,
        bool userInteractive,
        bool expected)
    {
        bool actual = TerminalCapabilities.ShouldUseInteractive(
            inputRedirected,
            outputRedirected,
            userInteractive);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Constructor_RejectsEmptyMenu()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TerminalMenuNavigator(itemCount: 0));
    }
}
