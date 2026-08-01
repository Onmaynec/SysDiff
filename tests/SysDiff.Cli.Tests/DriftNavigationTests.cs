namespace SysDiff.Cli.Tests;

public sealed class DriftNavigationTests
{
    [Theory]
    [InlineData(ConsoleKey.P, 0)]
    [InlineData(ConsoleKey.C, 1)]
    [InlineData(ConsoleKey.G, 2)]
    [InlineData(ConsoleKey.T, 3)]
    [InlineData(ConsoleKey.K, 4)]
    [InlineData(ConsoleKey.W, 5)]
    [InlineData(ConsoleKey.L, 6)]
    [InlineData(ConsoleKey.D, 8)]
    public void DriftHotkeys_OpenExpectedModule(ConsoleKey key, int expectedIndex)
    {
        int actual = TerminalMenuNavigator.ResolveDirectIndex(key, itemCount: 9);

        Assert.Equal(expectedIndex, actual);
    }

    [Theory]
    [InlineData(ConsoleKey.D1, 0)]
    [InlineData(ConsoleKey.D5, 4)]
    [InlineData(ConsoleKey.D9, 8)]
    public void NumericDeck_RemainsStable(ConsoleKey key, int expectedIndex)
    {
        Assert.Equal(expectedIndex, TerminalMenuNavigator.ResolveDirectIndex(key, 9));
    }
}
