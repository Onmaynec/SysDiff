namespace SysDiff.Cli;

public enum TerminalNavigationAction
{
    None,
    Activate,
    Back,
    Exit,
    Search,
    Filter,
    Sort,
    ToggleRaw,
    Export,
    Refresh
}

public sealed class TerminalMenuNavigator
{
    public TerminalMenuNavigator(int itemCount, int selectedIndex = 0)
    {
        if (itemCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemCount));
        }

        ItemCount = itemCount;
        SelectedIndex = Math.Clamp(selectedIndex, 0, itemCount - 1);
    }

    public int ItemCount { get; }

    public int SelectedIndex { get; private set; }

    public TerminalNavigationAction Apply(ConsoleKey key)
    {
        int directIndex = ResolveDirectIndex(key, ItemCount);
        if (directIndex >= 0)
        {
            SelectedIndex = directIndex;
            return TerminalNavigationAction.Activate;
        }

        switch (key)
        {
            case ConsoleKey.UpArrow:
                SelectedIndex = (SelectedIndex - 1 + ItemCount) % ItemCount;
                return TerminalNavigationAction.None;
            case ConsoleKey.DownArrow:
                SelectedIndex = (SelectedIndex + 1) % ItemCount;
                return TerminalNavigationAction.None;
            case ConsoleKey.Home:
                SelectedIndex = 0;
                return TerminalNavigationAction.None;
            case ConsoleKey.End:
                SelectedIndex = ItemCount - 1;
                return TerminalNavigationAction.None;
            case ConsoleKey.Enter:
            case ConsoleKey.Spacebar:
                return TerminalNavigationAction.Activate;
            case ConsoleKey.Escape:
            case ConsoleKey.Backspace:
                return TerminalNavigationAction.Back;
            case ConsoleKey.Q:
                return TerminalNavigationAction.Exit;
            case ConsoleKey.Oem2:
            case ConsoleKey.Divide:
                return TerminalNavigationAction.Search;
            case ConsoleKey.F:
                return TerminalNavigationAction.Filter;
            case ConsoleKey.S:
                return TerminalNavigationAction.Sort;
            case ConsoleKey.R:
                return TerminalNavigationAction.ToggleRaw;
            case ConsoleKey.E:
                return TerminalNavigationAction.Export;
            case ConsoleKey.F5:
                return TerminalNavigationAction.Refresh;
            default:
                return TerminalNavigationAction.None;
        }
    }

    public void SetSelectedIndex(int selectedIndex) =>
        SelectedIndex = Math.Clamp(selectedIndex, 0, ItemCount - 1);

    public static int ResolveDirectIndex(ConsoleKey key, int itemCount)
    {
        int index = key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => 0,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => 1,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => 2,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => 3,
            ConsoleKey.D5 or ConsoleKey.NumPad5 => 4,
            ConsoleKey.D6 or ConsoleKey.NumPad6 => 5,
            ConsoleKey.D7 or ConsoleKey.NumPad7 => 6,
            ConsoleKey.D8 or ConsoleKey.NumPad8 => 7,
            ConsoleKey.D9 or ConsoleKey.NumPad9 => 8,
            _ => -1
        };

        if (itemCount == 9)
        {
            index = key switch
            {
                ConsoleKey.P or ConsoleKey.B or ConsoleKey.A => 0,
                ConsoleKey.C => 1,
                ConsoleKey.G => 2,
                ConsoleKey.T => 3,
                ConsoleKey.K => 4,
                ConsoleKey.W => 5,
                ConsoleKey.L => 6,
                ConsoleKey.D => 8,
                _ => index
            };
        }

        return index >= 0 && index < itemCount ? index : -1;
    }
}

public static class TerminalCapabilities
{
    public static bool ShouldUseInteractive(
        bool inputRedirected,
        bool outputRedirected,
        bool userInteractive) =>
        userInteractive && !inputRedirected && !outputRedirected;

    public static bool IsInteractive => ShouldUseInteractive(
        Console.IsInputRedirected,
        Console.IsOutputRedirected,
        Environment.UserInteractive);

    public static int GetSafeWindowWidth()
    {
        try
        {
            return Math.Max(40, Console.WindowWidth - 1);
        }
        catch (IOException)
        {
            return 100;
        }
        catch (PlatformNotSupportedException)
        {
            return 100;
        }
    }

    public static int GetSafeWindowHeight()
    {
        try
        {
            return Math.Max(18, Console.WindowHeight);
        }
        catch (IOException)
        {
            return 30;
        }
        catch (PlatformNotSupportedException)
        {
            return 30;
        }
    }
}

internal sealed record TerminalMenuItem(
    string Id,
    string Title,
    string Description,
    string Glyph);

internal sealed class InlineProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public InlineProgress(Action<T> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void Report(T value) => _handler(value);
}

