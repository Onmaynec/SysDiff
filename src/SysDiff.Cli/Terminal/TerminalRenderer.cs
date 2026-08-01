using System.Diagnostics;
using SysDiff.Domain;

namespace SysDiff.Cli;

internal sealed partial class TerminalRenderer
{
    private readonly object _consoleLock = new();
    private readonly TerminalMotionPolicy _motion;
    private int _spinnerIndex;
    private long _lastProgressRenderTimestamp;

    public TerminalRenderer()
    {
        _motion = TerminalMotionPolicy.Detect();
    }

    public IDisposable EnterApplicationMode()
    {
        var session = new ConsoleSession();
        PlayBootSequence();
        return session;
    }

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
            int width = Math.Min(112, TerminalCapabilities.GetSafeWindowWidth());
            StartPanel($"INPUT NODE // {title}", width, CyberTheme.Accent);
            WriteLine("[ CHANNEL:KEYBOARD ]  [ MODE:TEXT ]", width, CyberTheme.Muted);
            Separator(width);
            WriteWrapped(prompt, width, CyberTheme.Text);
            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                WriteWrapped($"DEFAULT > {defaultValue}", width, CyberTheme.Muted);
            }

            Separator(width);
            WriteLine("Введите значение и нажмите Enter · Esc не используется в режиме ввода", width, CyberTheme.Muted, centered: true);
            EndPanel(width);
            SetCursorVisible(true);
            SetColor(CyberTheme.Accent);
            Console.Write("SYS> ");
            ResetColor();
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

                ShowMessage("INPUT VALIDATION", "Значение не может быть пустым.", MessageKind.Warning);
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
                "INPUT VALIDATION",
                $"Введите целое число от {minimum:N0} до {maximum:N0}.",
                MessageKind.Warning);
        }
    }

    public bool Confirm(string title, string question, bool defaultValue = false)
    {
        string yes = "AUTHORIZE";
        string no = "ABORT";
        string[] choices = defaultValue ? [yes, no] : [no, yes];
        string? answer = Select(
            $"CONFIRMATION // {title}",
            question,
            choices,
            value => value == yes ? "[ YES ] AUTHORIZE ACTION" : "[ NO  ] ABORT AND RETURN",
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
            int width = Math.Min(112, TerminalCapabilities.GetSafeWindowWidth());
            ConsoleColor color = kind switch
            {
                MessageKind.Success => CyberTheme.Success,
                MessageKind.Warning => CyberTheme.Warning,
                MessageKind.Error => CyberTheme.Error,
                _ => CyberTheme.Secondary
            };
            string marker = kind switch
            {
                MessageKind.Success => "[ OK ]",
                MessageKind.Warning => "[ !! ]",
                MessageKind.Error => "[ XX ]",
                _ => "[ INFO ]"
            };
            StartPanel($"{marker} {title}", width, color);
            WriteLine($"TIMESTAMP {DateTimeOffset.Now:HH:mm:ss} · NODE {Environment.MachineName}", width, CyberTheme.Muted);
            Separator(width, color);
            WriteWrapped(message, width, CyberTheme.Text);
            if (pause)
            {
                Separator(width, color);
                WriteLine("PRESS ANY KEY TO RETURN TO CONTROL NODE", width, CyberTheme.Muted, centered: true);
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
        long timestamp = Stopwatch.GetTimestamp();
        double elapsedMilliseconds = (timestamp - _lastProgressRenderTimestamp) * 1000d / Stopwatch.Frequency;
        if (_lastProgressRenderTimestamp != 0 && elapsedMilliseconds < 60d)
        {
            return;
        }
        _lastProgressRenderTimestamp = timestamp;

        lock (_consoleLock)
        {
            RenderProviderStream(progress, _spinnerIndex++);
        }
    }

    public async Task<T> RunSpinnerAsync<T>(
        string title,
        string message,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var stopwatch = Stopwatch.StartNew();
        Task<T> task = operation();
        int tick = 0;

        try
        {
            while (!task.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_consoleLock)
                {
                    RenderActionConsole(title, message, CyberStageState.Running, stopwatch.Elapsed, tick);
                }
                int delay = _motion.AnimationsEnabled ? Math.Max(30, _motion.FrameDelayMilliseconds) : 80;
                await Task.WhenAny(task, Task.Delay(delay, cancellationToken));
                tick++;
            }

            T result = await task;
            lock (_consoleLock)
            {
                RenderActionConsole(title, message, CyberStageState.Completed, stopwatch.Elapsed, tick);
            }
            if (_motion.AnimationsEnabled)
            {
                await Task.Delay(140, CancellationToken.None);
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            lock (_consoleLock)
            {
                RenderActionConsole(title, message, CyberStageState.Cancelled, stopwatch.Elapsed, tick);
            }
            throw;
        }
        catch
        {
            lock (_consoleLock)
            {
                RenderActionConsole(title, message, CyberStageState.Failed, stopwatch.Elapsed, tick);
            }
            throw;
        }
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
        const int width = 104;
        Console.WriteLine($"┏{new string('━', width - 2)}┓");
        Console.WriteLine("┃                          SYSDIFF CYBER CONSOLE 0.5.0                           ┃");
        Console.WriteLine("┃ [ NODE:ONLINE ] [ ROOT:ADMIN ] [ MOTION:SAFE ] [ CHANNEL:LOCAL ]             ┃");
        Console.WriteLine("┃ [01] SNAPSHOT NODE   [02] DIFF LAB   [03] WATCH OPS   [04] LIVE SIGNAL       ┃");
        Console.WriteLine("┃ ACTION CONSOLE // ████████████████░░░░ // PROVIDER STREAM // COMMAND DECK    ┃");
        Console.WriteLine("┃ KEYS: 1-9 · P SNAPSHOT · C COMPARE · W WATCH · L LIVE · D DIAGNOSTICS · Q    ┃");
        Console.WriteLine($"┗{new string('━', width - 2)}┛");
    }

    private void PlayBootSequence()
    {
        if (!_motion.AnimationsEnabled)
        {
            return;
        }

        string[] steps =
        [
            "NEGOTIATING TERMINAL CHANNEL",
            "VERIFYING LOCAL STORAGE",
            "INDEXING SNAPSHOT PROVIDERS",
            "ARMING COMPARISON ENGINE",
            "SYNCING LIVE MONITORS",
            "CONTROL NODE ONLINE"
        ];

        for (int completed = 0; completed < steps.Length; completed++)
        {
            for (int frame = 0; frame < 3; frame++)
            {
                if (TryConsumeSkipKey())
                {
                    return;
                }
                RenderBootFrame(steps, completed, frame);
                Thread.Sleep(_motion.FrameDelayMilliseconds);
            }
        }
    }

    private bool TryConsumeSkipKey()
    {
        try
        {
            if (!Console.KeyAvailable)
            {
                return false;
            }
            Console.ReadKey(intercept: true);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void RenderActionConsole(
        string title,
        string message,
        CyberStageState state,
        TimeSpan elapsed,
        int tick)
    {
        Clear();
        int width = Math.Min(118, TerminalCapabilities.GetSafeWindowWidth());
        ConsoleColor stateColor = CyberTheme.StageColor(state);
        StartPanel($"ACTION CONSOLE // {title.ToUpperInvariant()}", width, stateColor);
        WriteLine(
            $"{CyberTheme.NodeBadge(true)} [ ELAPSED:{elapsed:mm\\:ss\.ff} ] [ PID:{Environment.ProcessId} ]",
            width,
            CyberTheme.Muted);
        Separator(width, stateColor);
        WriteStage("CONTROL CHANNEL", CyberStageState.Completed, width);
        WriteStage(message, state, width);
        WriteStage("COMMIT RESULT", state == CyberStageState.Completed ? CyberStageState.Completed : CyberStageState.Queued, width);
        Separator(width, stateColor);
        double progress = state switch
        {
            CyberStageState.Completed => 1d,
            CyberStageState.Failed or CyberStageState.Cancelled => 0.72d,
            _ => (Math.Abs(tick) % 80) / 80d
        };
        string bar = CyberAnimation.BuildProgressBar(progress, Math.Max(12, width - 28), tick);
        WriteLine($"STREAM {bar} {(int)(progress * 100),3}%", width, stateColor);
        WriteLine($"SCAN   {CyberAnimation.BuildScanner(tick, Math.Max(12, width - 11))}", width, CyberTheme.Secondary);
        WriteLine(
            state switch
            {
                CyberStageState.Completed => "RESULT > OPERATION COMMITTED SUCCESSFULLY",
                CyberStageState.Failed => "RESULT > OPERATION FAILED · ERROR CHANNEL OPEN",
                CyberStageState.Cancelled => "RESULT > OPERATION CANCELLED BY OPERATOR",
                _ => $"TRACE  > {CyberAnimation.Spinner(tick)} {message}"
            },
            width,
            stateColor);
        Separator(width, stateColor);
        WriteLine("CTRL+C ABORT · OUTPUT REMAINS LOCAL · NO SYSTEM COMMANDS EXECUTED FROM CAPTURED DATA", width, CyberTheme.Muted, centered: true);
        EndPanel(width, stateColor);
    }

    private void RenderProviderStream(SnapshotProgress progress, int tick)
    {
        Clear();
        int width = Math.Min(122, TerminalCapabilities.GetSafeWindowWidth());
        StartPanel("PROVIDER STREAM // SNAPSHOT CAPTURE", width, CyberTheme.Accent);
        WriteLine($"{CyberTheme.NodeBadge(true)} [ PROFILE:ACTIVE ] [ OBJECTS:{progress.Processed:N0} ]", width, CyberTheme.Muted);
        Separator(width);
        WriteStage("SNAPSHOT TRANSACTION", CyberStageState.Completed, width);
        WriteStage($"PROVIDER {progress.ProviderId}", CyberStageState.Running, width);
        WriteStage("SQLITE COMMIT", CyberStageState.Queued, width);
        Separator(width);
        string bar = CyberAnimation.BuildProgressBar((Math.Abs(tick) % 100) / 100d, Math.Max(12, width - 28), tick);
        WriteLine($"CAPTURE {bar}", width, CyberTheme.Accent);
        WriteLine($"MODULE  > {progress.Message}", width, CyberTheme.Secondary);
        WriteWrapped(
            string.IsNullOrWhiteSpace(progress.CurrentItem)
                ? "TRACE   > waiting for provider data"
                : $"TRACE   > {progress.CurrentItem}",
            width,
            CyberTheme.Text);
        Separator(width);
        WriteLine("LIVE STREAM · CTRL+C CANCEL · ACCESS ERRORS ARE ISOLATED PER PROVIDER", width, CyberTheme.Muted, centered: true);
        EndPanel(width);
    }

    private void WriteStage(string name, CyberStageState state, int width)
    {
        WriteLine(
            $"{CyberTheme.StageMarker(state)} {FitRaw(name.ToUpperInvariant(), Math.Max(12, width - 18))}",
            width,
            CyberTheme.StageColor(state));
    }

    private char SpinnerFrame()
    {
        char value = CyberAnimation.Spinner(_spinnerIndex);
        _spinnerIndex++;
        return value;
    }

    private void StartPanel(string title, int width, ConsoleColor color)
    {
        TopBorder(width, color);
        WriteLine(title, width, color, centered: true);
        Separator(width, color);
    }

    private void TopBorder(int width, ConsoleColor color)
    {
        SetColor(color);
        Console.WriteLine($"┏{new string('━', Math.Max(0, width - 2))}┓");
        ResetColor();
    }

    private void EndPanel(int width, ConsoleColor? color = null)
    {
        SetColor(color ?? CyberTheme.Border);
        Console.WriteLine($"┗{new string('━', Math.Max(0, width - 2))}┛");
        ResetColor();
    }

    private void Separator(int width, ConsoleColor? color = null)
    {
        SetColor(color ?? CyberTheme.Border);
        Console.WriteLine($"┣{new string('━', Math.Max(0, width - 2))}┫");
        ResetColor();
    }

    private void WriteLine(
        string text,
        int width,
        ConsoleColor color,
        bool centered = false,
        ConsoleColor? background = null)
    {
        int innerWidth = Math.Max(1, width - 2);
        string raw = FitRaw(text, innerWidth);
        string content = centered ? Center(raw, innerWidth) : raw.PadRight(innerWidth);
        SetColor(CyberTheme.Border);
        Console.Write('┃');
        SetColor(color, background);
        Console.Write(content);
        SetColor(CyberTheme.Border);
        Console.WriteLine('┃');
        ResetColor();
    }

    private void WriteWrapped(string text, int width, ConsoleColor color)
    {
        int innerWidth = Math.Max(1, width - 4);
        foreach (string line in Wrap(text, innerWidth))
        {
            WriteLine($" {line}", width, color);
        }
    }

    private void WriteKeyValue(
        string key,
        string value,
        int width,
        ConsoleColor? valueColor = null)
    {
        int keyWidth = Math.Min(24, Math.Max(12, width / 4));
        int valueWidth = Math.Max(8, width - keyWidth - 5);
        SetColor(CyberTheme.Border);
        Console.Write('┃');
        SetColor(CyberTheme.Muted);
        Console.Write(Fit($" {key}", keyWidth));
        SetColor(CyberTheme.Border);
        Console.Write('┃');
        SetColor(valueColor ?? CyberTheme.Text);
        Console.Write(Fit(value, valueWidth));
        SetColor(CyberTheme.Border);
        Console.WriteLine('┃');
        ResetColor();
    }

    private void WriteTransientLine(string text, ConsoleColor color, int width)
    {
        SetColor(color);
        Console.Write('\r');
        Console.Write(Fit(text, width));
        ResetColor();
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

    private void SetColor(ConsoleColor foreground, ConsoleColor? background = null)
    {
        if (!_motion.ColorsEnabled)
        {
            return;
        }

        try
        {
            Console.ForegroundColor = foreground;
            Console.BackgroundColor = background ?? ConsoleColor.Black;
        }
        catch (IOException)
        {
        }
    }

    private void ResetColor()
    {
        if (!_motion.ColorsEnabled)
        {
            return;
        }
        try
        {
            Console.ResetColor();
        }
        catch (IOException)
        {
        }
    }

    private void Clear()
    {
        try
        {
            ResetColor();
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
                Console.Title = "SysDiff 0.5.0 · Cyber Console";
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
