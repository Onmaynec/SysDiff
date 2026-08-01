using SysDiff.Domain;

namespace SysDiff.Cli;

internal sealed partial class TerminalRenderer
{
    private readonly object _consoleLock = new();
    private int _spinnerIndex;

    public IDisposable EnterApplicationMode() => new ConsoleSession();

    public T? Select<T>(
        string title,
        string subtitle,
        IReadOnlyList<T> items,
        Func<T, string> label,
        int selectedIndex = 0,
        bool allowBack = true)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(label);

        if (items.Count == 0)
        {
            ShowMessage(title, "Нет доступных элементов.", MessageKind.Warning);
            return default;
        }

        var navigator = new TerminalMenuNavigator(items.Count, selectedIndex);
        while (true)
        {
            RenderSelection(title, subtitle, items, label, navigator.SelectedIndex, allowBack);
            TerminalNavigationAction action = navigator.Apply(ReadKey().Key);
            if (action == TerminalNavigationAction.Activate)
            {
                return items[navigator.SelectedIndex];
            }

            if (allowBack && action is TerminalNavigationAction.Back or TerminalNavigationAction.Exit)
            {
                return default;
            }
        }
    }

    public string? ReadText(
        string title,
        string prompt,
        string? defaultValue = null,
        bool allowEmpty = false)
    {
        while (true)
        {
            Clear();
            int width = Math.Min(110, TerminalCapabilities.GetSafeWindowWidth());
            StartPanel(title, width, ConsoleColor.Cyan);
            WriteWrapped(prompt, width, ConsoleColor.Gray);
            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                WriteWrapped($"По умолчанию: {defaultValue}", width, ConsoleColor.DarkGray);
            }

            Separator(width);
            WriteLine("Введите значение и нажмите Enter", width, ConsoleColor.DarkGray, centered: true);
            EndPanel(width);
            SetCursorVisible(true);
            Console.Write("> ");
            string? value = Console.ReadLine();
            SetCursorVisible(false);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (!string.IsNullOrWhiteSpace(defaultValue))
                {
                    return defaultValue;
                }

                if (allowEmpty)
                {
                    return string.Empty;
                }

                ShowMessage("Проверка ввода", "Значение не может быть пустым.", MessageKind.Warning);
                continue;
            }

            return value.Trim();
        }
    }

    public int ReadNumber(
        string title,
        string prompt,
        int defaultValue,
        int minimum,
        int maximum)
    {
        while (true)
        {
            string? text = ReadText(
                title,
                prompt,
                defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (int.TryParse(text, out int value) && value >= minimum && value <= maximum)
            {
                return value;
            }

            ShowMessage(
                "Проверка ввода",
                $"Введите целое число от {minimum:N0} до {maximum:N0}.",
                MessageKind.Warning);
        }
    }

    public bool Confirm(string title, string question, bool defaultValue = false)
    {
        string yes = "Да";
        string no = "Нет";
        string[] choices = defaultValue ? [yes, no] : [no, yes];
        string? answer = Select(
            title,
            question,
            choices,
            value => value,
            allowBack: true);
        return string.Equals(answer, yes, StringComparison.Ordinal);
    }

    public void ShowMessage(
        string title,
        string message,
        MessageKind kind = MessageKind.Info,
        bool pause = true)
    {
        lock (_consoleLock)
        {
            Clear();
            int width = Math.Min(110, TerminalCapabilities.GetSafeWindowWidth());
            ConsoleColor color = kind switch
            {
                MessageKind.Success => ConsoleColor.Green,
                MessageKind.Warning => ConsoleColor.Yellow,
                MessageKind.Error => ConsoleColor.Red,
                _ => ConsoleColor.Cyan
            };
            StartPanel(title, width, color);
            WriteWrapped(message, width, ConsoleColor.Gray);
            if (pause)
            {
                Separator(width, color);
                WriteLine("Нажмите любую клавишу", width, ConsoleColor.DarkGray, centered: true);
            }
            EndPanel(width, color);
        }

        if (pause)
        {
            ReadKey();
        }
    }

    public void ShowException(string title, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ShowMessage(title, exception.Message, MessageKind.Error);
    }

    public void ShowSnapshotProgress(SnapshotProgress progress)
    {
        lock (_consoleLock)
        {
            int width = Math.Min(120, TerminalCapabilities.GetSafeWindowWidth());
            string current = string.IsNullOrWhiteSpace(progress.CurrentItem)
                ? string.Empty
                : $" · {FitRaw(progress.CurrentItem, Math.Max(12, width - 42))}";
            WriteTransientLine(
                $"{SpinnerFrame()} [{progress.ProviderId}] {progress.Processed:N0} объектов{current}",
                ConsoleColor.Cyan,
                width);
        }
    }

    public async Task<T> RunSpinnerAsync<T>(
        string title,
        string message,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Clear();
        int width = Math.Min(110, TerminalCapabilities.GetSafeWindowWidth());
        StartPanel(title, width, ConsoleColor.Cyan);
        WriteWrapped(message, width, ConsoleColor.Gray);
        EndPanel(width);

        Task<T> task = operation();
        while (!task.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_consoleLock)
            {
                WriteTransientLine($"{SpinnerFrame()} {message}", ConsoleColor.Cyan, width);
            }
            await Task.WhenAny(task, Task.Delay(80, cancellationToken));
        }

        Console.WriteLine();
        return await task;
    }

    public async Task RunSpinnerAsync(
        string title,
        string message,
        Func<Task> operation,
        CancellationToken cancellationToken)
    {
        await RunSpinnerAsync(
            title,
            message,
            async () =>
            {
                await operation();
                return true;
            },
            cancellationToken);
    }

    public void RenderSmokeFrame()
    {
        const int width = 100;
        Console.WriteLine(new string('=', width));
        Console.WriteLine("SYSDIFF CONTROL CENTER 0.4.0");
        Console.WriteLine("Snapshot Center | Comparison Lab | Watch Session | Live Monitor | Diagnostics");
        Console.WriteLine("ARROWS ENTER ESC Q");
        Console.WriteLine(new string('=', width));
    }

    private char SpinnerFrame()
    {
        const string frames = "|/-\\";
        char value = frames[_spinnerIndex % frames.Length];
        _spinnerIndex++;
        return value;
    }

    private static void StartPanel(string title, int width, ConsoleColor color)
    {
        TopBorder(width, color);
        WriteLine(title, width, color, centered: true);
        Separator(width, color);
    }

    private static void TopBorder(int width, ConsoleColor color)
    {
        SetColor(color);
        Console.WriteLine($"╭{new string('─', Math.Max(0, width - 2))}╮");
        Console.ResetColor();
    }

    private static void EndPanel(int width, ConsoleColor color = ConsoleColor.DarkCyan)
    {
        SetColor(color);
        Console.WriteLine($"╰{new string('─', Math.Max(0, width - 2))}╯");
        Console.ResetColor();
    }

    private static void Separator(int width, ConsoleColor color = ConsoleColor.DarkCyan)
    {
        SetColor(color);
        Console.WriteLine($"├{new string('─', Math.Max(0, width - 2))}┤");
        Console.ResetColor();
    }

    private static void WriteLine(
        string text,
        int width,
        ConsoleColor color,
        bool centered = false,
        ConsoleColor? background = null)
    {
        int innerWidth = Math.Max(1, width - 2);
        string raw = FitRaw(text, innerWidth);
        string content = centered ? Center(raw, innerWidth) : raw.PadRight(innerWidth);
        SetColor(ConsoleColor.DarkCyan);
        Console.Write('│');
        SetColor(color, background);
        Console.Write(content);
        SetColor(ConsoleColor.DarkCyan);
        Console.WriteLine('│');
        Console.ResetColor();
    }

    private static void WriteWrapped(string text, int width, ConsoleColor color)
    {
        int innerWidth = Math.Max(1, width - 4);
        foreach (string line in Wrap(text, innerWidth))
        {
            WriteLine($" {line}", width, color);
        }
    }

    private static void WriteKeyValue(
        string key,
        string value,
        int width,
        ConsoleColor valueColor = ConsoleColor.Gray)
    {
        int keyWidth = Math.Min(24, Math.Max(12, width / 4));
        int valueWidth = Math.Max(8, width - keyWidth - 5);
        SetColor(ConsoleColor.DarkCyan);
        Console.Write('│');
        SetColor(ConsoleColor.DarkGray);
        Console.Write(Fit($" {key}", keyWidth));
        SetColor(ConsoleColor.DarkCyan);
        Console.Write('│');
        SetColor(valueColor);
        Console.Write(Fit(value, valueWidth));
        SetColor(ConsoleColor.DarkCyan);
        Console.WriteLine('│');
        Console.ResetColor();
    }

    private static void WriteTransientLine(string text, ConsoleColor color, int width)
    {
        SetColor(color);
        Console.Write('\r');
        Console.Write(Fit(text, width));
        Console.ResetColor();
    }

    private static IEnumerable<string> Wrap(string value, int width)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield return string.Empty;
            yield break;
        }

        foreach (string paragraph in value.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            string current = paragraph.Trim();
            while (current.Length > width)
            {
                int breakAt = current.LastIndexOf(' ', width);
                if (breakAt <= 0)
                {
                    breakAt = width;
                }
                yield return current[..breakAt].TrimEnd();
                current = current[breakAt..].TrimStart();
            }
            yield return current;
        }
    }

    private static string Center(string value, int width)
    {
        int left = Math.Max(0, (width - value.Length) / 2);
        return new string(' ', left) + value + new string(' ', Math.Max(0, width - left - value.Length));
    }

    private static string Fit(string? value, int width) =>
        FitRaw(value ?? string.Empty, width).PadRight(Math.Max(0, width));

    private static string FitRaw(string value, int width)
    {
        if (width <= 0)
        {
            return string.Empty;
        }
        string sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        return sanitized.Length <= width
            ? sanitized
            : width == 1 ? "…" : sanitized[..(width - 1)] + "…";
    }

    private static void SetColor(ConsoleColor foreground, ConsoleColor? background = null)
    {
        try
        {
            Console.ForegroundColor = foreground;
            Console.BackgroundColor = background ?? ConsoleColor.Black;
        }
        catch (IOException)
        {
        }
    }

    private static void Clear()
    {
        try
        {
            Console.ResetColor();
            Console.Clear();
        }
        catch (IOException)
        {
            Console.WriteLine();
        }
    }

    private static ConsoleKeyInfo ReadKey()
    {
        try
        {
            return Console.ReadKey(intercept: true);
        }
        catch (InvalidOperationException)
        {
            return new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false);
        }
    }

    private static void SetCursorVisible(bool visible)
    {
        try
        {
            Console.CursorVisible = visible;
        }
        catch (IOException)
        {
        }
    }

    private sealed class ConsoleSession : IDisposable
    {
        private readonly ConsoleColor _foreground;
        private readonly ConsoleColor _background;
        private readonly bool _cursorVisible;
        private bool _disposed;

        public ConsoleSession()
        {
            _foreground = Console.ForegroundColor;
            _background = Console.BackgroundColor;
            try
            {
                _cursorVisible = Console.CursorVisible;
                Console.CursorVisible = false;
                Console.Title = "SysDiff 0.4.0 · Terminal Control Center";
                Console.Clear();
            }
            catch (IOException)
            {
                _cursorVisible = true;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            try
            {
                Console.ForegroundColor = _foreground;
                Console.BackgroundColor = _background;
                Console.CursorVisible = _cursorVisible;
                Console.Clear();
            }
            catch (IOException)
            {
            }
        }
    }
}

internal enum MessageKind
{
    Info,
    Success,
    Warning,
    Error
}

internal enum TerminalDiagnosticState
{
    Info,
    Ok,
    Warning,
    Error
}

internal sealed record TerminalDiagnosticItem(
    TerminalDiagnosticState State,
    string Name,
    string Value);

internal sealed record TerminalDashboardState(
    int SnapshotCount,
    int ReportCount,
    int ProviderCount,
    bool IsAdministrator,
    bool PortableMode,
    string WindowsVersion,
    string DataDirectory);
