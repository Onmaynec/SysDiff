using System.Text;
using SysDiff.Domain;

namespace SysDiff.Cli;

internal sealed class TerminalRenderer
{
    private static readonly string[] Logo =
    [
        "███████╗██╗   ██╗███████╗██████╗ ██╗███████╗███████╗",
        "██╔════╝╚██╗ ██╔╝██╔════╝██╔══██╗██║██╔════╝██╔════╝",
        "███████╗ ╚████╔╝ ███████╗██║  ██║██║█████╗  █████╗  ",
        "╚════██║  ╚██╔╝  ╚════██║██║  ██║██║██╔══╝  ██╔══╝  ",
        "███████║   ██║   ███████║██████╔╝██║██║     ██║     ",
        "╚══════╝   ╚═╝   ╚══════╝╚═════╝ ╚═╝╚═╝     ╚═╝     "
    ];

    private readonly object _consoleLock = new();
    private int _spinnerIndex;

    public IDisposable EnterApplicationMode() => new ConsoleSession();

    public void RenderDashboard(
        IReadOnlyList<TerminalMenuItem> menu,
        int selectedIndex,
        TerminalDashboardState state)
    {
        lock (_consoleLock)
        {
            Clear();
            int width = Math.Min(132, TerminalCapabilities.GetSafeWindowWidth());
            bool compact = width < 92;

            WriteTopBorder(width, ConsoleColor.DarkCyan);
            if (compact)
            {
                WriteSingleLine("SYSDIFF CONTROL CENTER", width, ConsoleColor.Cyan, centered: true);
                WriteSingleLine("Windows investigation console · v0.4.0", width, ConsoleColor.DarkGray, centered: true);
            }
            else
            {
                foreach (string line in Logo)
                {
                    WriteSingleLine(line, width, ConsoleColor.Cyan, centered: true);
                }

                WriteSingleLine("TERMINAL CONTROL CENTER · VERSION 0.4.0", width, ConsoleColor.DarkCyan, centered: true);
            }

            WriteSeparator(width, ConsoleColor.DarkCyan);

            if (compact)
            {
                RenderCompactMenu(menu, selectedIndex, state, width);
            }
            else
            {
                RenderWideMenu(menu, selectedIndex, state, width);
            }

            WriteSeparator(width, ConsoleColor.DarkCyan);
            WriteSingleLine(
                "↑ ↓  навигация    Enter  открыть    Esc  назад    F5  обновить    Q  выход",
                width,
                ConsoleColor.DarkGray,
                centered: true);
            WriteBottomBorder(width, ConsoleColor.DarkCyan);
        }
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
            ConsoleKeyInfo key = ReadKey();
            TerminalNavigationAction action = navigator.Apply(key.Key);
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
            WriteTopBorder(width, ConsoleColor.DarkCyan);
            WriteSingleLine(title, width, ConsoleColor.Cyan, centered: true);
            WriteSeparator(width, ConsoleColor.DarkCyan);
            WriteWrapped(prompt, width, ConsoleColor.Gray);
            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                WriteWrapped($"По умолчанию: {defaultValue}", width, ConsoleColor.DarkGray);
            }

            WriteSeparator(width, ConsoleColor.DarkCyan);
            WriteSingleLine("Введите значение и нажмите Enter · пустая строка = значение по умолчанию", width, ConsoleColor.DarkGray);
            WriteBottomBorder(width, ConsoleColor.DarkCyan);
            SetCursorVisible(true);
            SetColor(ConsoleColor.White);
            Console.Write("> ");
            string? value = Console.ReadLine();
            SetCursorVisible(false);
            Console.ResetColor();

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
            string? text = ReadText(title, prompt, defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
        string? answer = Select(
            title,
            question,
            defaultValue ? [yes, no] : [no, yes],
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

            WriteTopBorder(width, color);
            WriteSingleLine(title, width, color, centered: true);
            WriteSeparator(width, color);
            WriteWrapped(message, width, ConsoleColor.Gray);
            if (pause)
            {
                WriteSeparator(width, color);
                WriteSingleLine("Нажмите любую клавишу, чтобы продолжить", width, ConsoleColor.DarkGray, centered: true);
            }

            WriteBottomBorder(width, color);
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
            char spinner = SpinnerFrame();
            string current = string.IsNullOrWhiteSpace(progress.CurrentItem)
                ? string.Empty
                : $" · {Fit(progress.CurrentItem, Math.Max(12, width - 43))}";
            string text = $"{spinner} [{progress.ProviderId}] {progress.Processed:N0} объектов{current}";
            WriteTransientLine(text, ConsoleColor.Cyan, width);
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
        WriteTopBorder(width, ConsoleColor.DarkCyan);
        WriteSingleLine(title, width, ConsoleColor.Cyan, centered: true);
        WriteSeparator(width, ConsoleColor.DarkCyan);
        WriteSingleLine(message, width, ConsoleColor.Gray);
        WriteBottomBorder(width, ConsoleColor.DarkCyan);

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

        lock (_consoleLock)
        {
            Console.WriteLine();
        }

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

    public void RenderSnapshotDetails(SnapshotRecord snapshot)
    {
        Clear();
        int width = Math.Min(122, TerminalCapabilities.GetSafeWindowWidth());
        WriteTopBorder(width, ConsoleColor.DarkCyan);
        WriteSingleLine($"SNAPSHOT · {snapshot.Name}", width, ConsoleColor.Cyan, centered: true);
        WriteSeparator(width, ConsoleColor.DarkCyan);
        WriteKeyValue("ID", snapshot.Id.ToString(), width);
        WriteKeyValue("Создан", snapshot.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"), width);
        WriteKeyValue("Профиль", snapshot.ProfileName, width);
        WriteKeyValue("Статус", snapshot.Status.ToString(), width, StatusColor(snapshot.Status));
        WriteKeyValue("Объектов", snapshot.Artifacts.Count.ToString("N0"), width);
        WriteKeyValue("Windows", $"{snapshot.WindowsEdition} · {snapshot.WindowsBuild}", width);
        WriteSeparator(width, ConsoleColor.DarkCyan);
        WriteSingleLine("ПРОВАЙДЕРЫ", width, ConsoleColor.DarkCyan);
        foreach (ProviderSnapshotResult result in snapshot.ProviderResults.OrderBy(value => value.DisplayName))
        {
            ConsoleColor color = result.Status switch
            {
                ProviderStatus.Success => ConsoleColor.Green,
                ProviderStatus.Partial => ConsoleColor.Yellow,
                ProviderStatus.Failed => ConsoleColor.Red,
                _ => ConsoleColor.DarkGray
            };
            WriteSingleLine(
                $"{ProviderMarker(result.Status),-6} {Fit(result.DisplayName, 32),-32} {result.ArtifactCount,12:N0} объектов",
                width,
                color);
        }

        WriteSeparator(width, ConsoleColor.DarkCyan);
        WriteSingleLine("Esc/любая клавиша · назад", width, ConsoleColor.DarkGray, centered: true);
        WriteBottomBorder(width, ConsoleColor.DarkCyan);
        ReadKey();
    }

    public void RenderComparisonSummary(
        SnapshotRecord before,
        SnapshotRecord after,
        ComparisonResult comparison)
    {
        Clear();
        int width = Math.Min(122, TerminalCapabilities.GetSafeWindowWidth());
        WriteTopBorder(width, ConsoleColor.DarkCyan);
        WriteSingleLine("COMPARISON LAB · OVERVIEW", width, ConsoleColor.Cyan, centered: true);
        WriteSeparator(width, ConsoleColor.DarkCyan);
        WriteKeyValue("Before", $"{before.Name} · {before.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}", width);
        WriteKeyValue("After", $"{after.Name} · {after.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}", width);
        WriteKeyValue("Noise", comparison.NoiseMode.ToString(), width);
        WriteKeyValue("Cross-machine", comparison.CrossMachine ? "да" : "нет", width, comparison.CrossMachine ? ConsoleColor.Yellow : ConsoleColor.Green);
        WriteSeparator(width, ConsoleColor.DarkCyan);

        foreach (ChangeType type in Enum.GetValues<ChangeType>())
        {
            int count = comparison.Changes.Count(change => change.ChangeType == type);
            if (count == 0)
            {
                continue;
            }

            WriteKeyValue(type.ToString(), count.ToString("N0"), width, ChangeColor(type));
        }

        WriteSeparator(width, ConsoleColor.DarkCyan);
        foreach (Severity severity in Enum.GetValues<Severity>().OrderByDescending(value => value))
        {
            int count = comparison.Changes.Count(change => change.Severity == severity);
            if (count > 0)
            {
                WriteKeyValue($"Severity {severity}", count.ToString("N0"), width, SeverityColor(severity));
            }
        }

        WriteKeyValue("Скрыто как шум", comparison.HiddenAsNoise.ToString("N0"), width, ConsoleColor.DarkGray);
        foreach (string warning in comparison.Warnings)
        {
            WriteWrapped($"Предупреждение: {warning}", width, ConsoleColor.Yellow);
        }

        WriteSeparator(width, ConsoleColor.DarkCyan);
        WriteSingleLine("Нажмите любую клавишу", width, ConsoleColor.DarkGray, centered: true);
        WriteBottomBorder(width, ConsoleColor.DarkCyan);
        ReadKey();
    }

    public void RenderChangeBrowser(
        IReadOnlyList<SystemChange> changes,
        int selectedIndex,
        string query,
        Severity minimumSeverity,
        bool severitySort,
        bool rawMode)
    {
        lock (_consoleLock)
        {
            Clear();
            int width = Math.Min(140, TerminalCapabilities.GetSafeWindowWidth());
            int height = TerminalCapabilities.GetSafeWindowHeight();
            bool wide = width >= 108;
            WriteTopBorder(width, ConsoleColor.DarkCyan);
            WriteSingleLine("COMPARISON LAB · CHANGE EXPLORER", width, ConsoleColor.Cyan, centered: true);
            WriteSingleLine(
                $"Найдено: {changes.Count:N0} · min severity: {minimumSeverity} · sort: {(severitySort ? "severity" : "provider")} · raw: {(rawMode ? "on" : "off")} · search: {query}",
                width,
                ConsoleColor.DarkGray);
            WriteSeparator(width, ConsoleColor.DarkCyan);

            int availableRows = Math.Max(5, height - 12);
            int start = Math.Max(0, selectedIndex - availableRows / 2);
            int end = Math.Min(changes.Count, start + availableRows);
            if (end - start < availableRows)
            {
                start = Math.Max(0, end - availableRows);
            }

            if (changes.Count == 0)
            {
                WriteSingleLine("Нет изменений для текущего фильтра.", width, ConsoleColor.Yellow, centered: true);
            }
            else if (wide)
            {
                int leftWidth = Math.Max(44, width / 2);
                for (int index = start; index < end; index++)
                {
                    SystemChange change = changes[index];
                    bool selected = index == selectedIndex;
                    string left = $"{(selected ? '▶' : ' ')} [{change.Severity}] {change.ChangeType,-8} {Fit(change.DisplayName, leftWidth - 27)}";
                    string right = selected
                        ? $"{change.ProviderId} · confidence {change.Confidence:P0}"
                        : string.Empty;
                    WriteSplitLine(left, right, width, leftWidth, selected ? ConsoleColor.Black : SeverityColor(change.Severity), selected ? ConsoleColor.Cyan : ConsoleColor.DarkGray, selected);
                }

                SystemChange selectedChange = changes[Math.Clamp(selectedIndex, 0, changes.Count - 1)];
                WriteSeparator(width, ConsoleColor.DarkCyan);
                WriteWrapped(selectedChange.Explanation, width, SeverityColor(selectedChange.Severity));
                WriteWrapped(selectedChange.WhyThisMatters, width, ConsoleColor.Gray);
                foreach (PropertyChange property in selectedChange.ChangedProperties.Take(3))
                {
                    WriteWrapped(
                        $"{property.Name}: {property.Before?.Value ?? "∅"}  →  {property.After?.Value ?? "∅"}",
                        width,
                        ConsoleColor.DarkGray);
                }
            }
            else
            {
                for (int index = start; index < end; index++)
                {
                    SystemChange change = changes[index];
                    bool selected = index == selectedIndex;
                    WriteSingleLine(
                        $"{(selected ? '▶' : ' ')} [{change.Severity}] {change.ChangeType} {Fit(change.DisplayName, width - 28)}",
                        width,
                        selected ? ConsoleColor.Black : SeverityColor(change.Severity),
                        background: selected ? ConsoleColor.Cyan : null);
                }
            }

            WriteSeparator(width, ConsoleColor.DarkCyan);
            WriteSingleLine(
                "↑↓ выбор  / поиск  F severity  S сортировка  R raw  E экспорт  Esc назад",
                width,
                ConsoleColor.DarkGray,
                centered: true);
            WriteBottomBorder(width, ConsoleColor.DarkCyan);
        }
    }

    public void RenderLiveEvents(string title, IReadOnlyList<LiveEvent> events)
    {
        Clear();
        int width = Math.Min(132, TerminalCapabilities.GetSafeWindowWidth());
        WriteTopBorder(width, ConsoleColor.DarkCyan);
        WriteSingleLine(title, width, ConsoleColor.Cyan, centered: true);
        WriteSeparator(width, ConsoleColor.DarkCyan);
        WriteKeyValue("Событий", events.Count.ToString("N0"), width);
        foreach (IGrouping<string, LiveEvent> group in events.GroupBy(value => value.EventType).OrderBy(value => value.Key))
        {
            WriteKeyValue(group.Key, group.Count().ToString("N0"), width, ConsoleColor.DarkCyan);
        }

        WriteSeparator(width, ConsoleColor.DarkCyan);
        foreach (LiveEvent item in events.TakeLast(14))
        {
            WriteSingleLine(
                $"{item.TimestampUtc.ToLocalTime():HH:mm:ss}  {item.EventType,-8}  {Fit(item.DisplayName, Math.Max(12, width - 35))}",
                width,
                item.EventType is "Started" or "Opened" ? ConsoleColor.Green : ConsoleColor.Yellow);
        }

        WriteSeparator(width, ConsoleColor.DarkCyan);
        WriteSingleLine("Нажмите любую клавишу", width, ConsoleColor.DarkGray, centered: true);
        WriteBottomBorder(width, ConsoleColor.DarkCyan);
        ReadKey();
    }

    public void RenderDiagnostics(IReadOnlyList<TerminalDiagnosticItem> diagnostics)
    {
        Clear();
        int width = Math.Min(118, TerminalCapabilities.GetSafeWindowWidth());
        WriteTopBorder(width, ConsoleColor.DarkCyan);
        WriteSingleLine("SYSDIFF DIAGNOSTICS", width, ConsoleColor.Cyan, centered: true);
        WriteSeparator(width, ConsoleColor.DarkCyan);
        foreach (TerminalDiagnosticItem item in diagnostics)
        {
            ConsoleColor color = item.State switch
            {
                TerminalDiagnosticState.Ok => ConsoleColor.Green,
                TerminalDiagnosticState.Warning => ConsoleColor.Yellow,
                TerminalDiagnosticState.Error => ConsoleColor.Red,
                _ => ConsoleColor.Gray
            };
            WriteSingleLine($"[{DiagnosticMarker(item.State),-4}] {item.Name,-24} {item.Value}", width, color);
        }

        WriteSeparator(width, ConsoleColor.DarkCyan);
        WriteSingleLine("Нажмите любую клавишу", width, ConsoleColor.DarkGray, centered: true);
        WriteBottomBorder(width, ConsoleColor.DarkCyan);
        ReadKey();
    }

    public void RenderSmokeFrame()
    {
        int width = 100;
        Console.WriteLine(new string('=', width));
        Console.WriteLine("SYSDIFF CONTROL CENTER 0.4.0");
        Console.WriteLine("Snapshot Center | Comparison Lab | Watch Session | Live Monitor | Diagnostics");
        Console.WriteLine("ARROWS ENTER ESC Q");
        Console.WriteLine(new string('=', width));
    }

    public static ConsoleColor SeverityColor(Severity severity) => severity switch
    {
        Severity.Critical => ConsoleColor.Red,
        Severity.High => ConsoleColor.Red,
        Severity.Medium => ConsoleColor.Yellow,
        Severity.Low => ConsoleColor.Cyan,
        _ => ConsoleColor.DarkGray
    };

    public static ConsoleColor ChangeColor(ChangeType type) => type switch
    {
        ChangeType.Added => ConsoleColor.Green,
        ChangeType.Removed => ConsoleColor.Red,
        ChangeType.Modified => ConsoleColor.Magenta,
        ChangeType.Moved or ChangeType.Renamed => ConsoleColor.Cyan,
        _ => ConsoleColor.Gray
    };

    private void RenderSelection<T>(
        string title,
        string subtitle,
        IReadOnlyList<T> items,
        Func<T, string> label,
        int selectedIndex,
        bool allowBack)
    {
        lock (_consoleLock)
        {
            Clear();
            int width = Math.Min(112, TerminalCapabilities.GetSafeWindowWidth());
            int height = TerminalCapabilities.GetSafeWindowHeight();
            WriteTopBorder(width, ConsoleColor.DarkCyan);
            WriteSingleLine(title, width, ConsoleColor.Cyan, centered: true);
            WriteWrapped(subtitle, width, ConsoleColor.DarkGray);
            WriteSeparator(width, ConsoleColor.DarkCyan);

            int rows = Math.Max(5, height - 10);
            int start = Math.Max(0, selectedIndex - rows / 2);
            int end = Math.Min(items.Count, start + rows);
            if (end - start < rows)
            {
                start = Math.Max(0, end - rows);
            }

            for (int index = start; index < end; index++)
            {
                bool selected = index == selectedIndex;
                string value = $"{(selected ? '▶' : ' ')}  {label(items[index])}";
                WriteSingleLine(
                    value,
                    width,
                    selected ? ConsoleColor.Black : ConsoleColor.Gray,
                    background: selected ? ConsoleColor.Cyan : null);
            }

            WriteSeparator(width, ConsoleColor.DarkCyan);
            WriteSingleLine(
                allowBack ? "↑ ↓  выбор    Enter  открыть    Esc  назад" : "↑ ↓  выбор    Enter  подтвердить",
                width,
                ConsoleColor.DarkGray,
                centered: true);
            WriteBottomBorder(width, ConsoleColor.DarkCyan);
        }
    }

    private void RenderCompactMenu(
        IReadOnlyList<TerminalMenuItem> menu,
        int selectedIndex,
        TerminalDashboardState state,
        int width)
    {
        WriteSingleLine(
            $"Windows {state.WindowsVersion} · {(state.IsAdministrator ? "Administrator" : "User")} · snapshots {state.SnapshotCount:N0}",
            width,
            state.IsAdministrator ? ConsoleColor.Green : ConsoleColor.Yellow);
        WriteSeparator(width, ConsoleColor.DarkCyan);
        for (int index = 0; index < menu.Count; index++)
        {
            TerminalMenuItem item = menu[index];
            bool selected = index == selectedIndex;
            WriteSingleLine(
                $"{(selected ? '▶' : ' ')} {item.Glyph} {item.Title} — {item.Description}",
                width,
                selected ? ConsoleColor.Black : ConsoleColor.Gray,
                background: selected ? ConsoleColor.Cyan : null);
        }
    }

    private void RenderWideMenu(
        IReadOnlyList<TerminalMenuItem> menu,
        int selectedIndex,
        TerminalDashboardState state,
        int width)
    {
        int leftWidth = 45;
        List<string> overview =
        [
            "SYSTEM OVERVIEW",
            $"Windows: {state.WindowsVersion}",
            $"Architecture: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}",
            $"Privileges: {(state.IsAdministrator ? "Administrator" : "Standard user")}",
            $"Storage: {(state.PortableMode ? "Portable" : "User profile")}",
            $"Snapshots: {state.SnapshotCount:N0}",
            $"Reports: {state.ReportCount:N0}",
            $"Providers: {state.ProviderCount:N0}",
            string.Empty,
            "DATA DIRECTORY",
            Fit(state.DataDirectory, width - leftWidth - 7)
        ];

        int rows = Math.Max(menu.Count, overview.Count);
        for (int index = 0; index < rows; index++)
        {
            string left = string.Empty;
            ConsoleColor leftColor = ConsoleColor.Gray;
            ConsoleColor? leftBackground = null;
            if (index < menu.Count)
            {
                TerminalMenuItem item = menu[index];
                bool selected = index == selectedIndex;
                left = $"{(selected ? '▶' : ' ')} {item.Glyph} {item.Title}";
                leftColor = selected ? ConsoleColor.Black : ConsoleColor.Gray;
                leftBackground = selected ? ConsoleColor.Cyan : null;
            }

            string right = index < overview.Count ? overview[index] : string.Empty;
            ConsoleColor rightColor = index is 0 or 9
                ? ConsoleColor.DarkCyan
                : index == 3
                    ? state.IsAdministrator ? ConsoleColor.Green : ConsoleColor.Yellow
                    : ConsoleColor.Gray;
            WriteSplitLine(left, right, width, leftWidth, leftColor, rightColor, leftBackground is not null);
        }
    }

    private void WriteSplitLine(
        string left,
        string right,
        int width,
        int leftWidth,
        ConsoleColor leftColor,
        ConsoleColor rightColor,
        bool selected)
    {
        int rightWidth = width - leftWidth - 3;
        SetColor(ConsoleColor.DarkCyan);
        Console.Write('│');
        SetColor(leftColor, selected ? ConsoleColor.Cyan : null);
        Console.Write(Fit(left, leftWidth));
        SetColor(ConsoleColor.DarkCyan);
        Console.Write('│');
        SetColor(rightColor);
        Console.Write(Fit(right, rightWidth));
        SetColor(ConsoleColor.DarkCyan);
        Console.WriteLine('│');
        Console.ResetColor();
    }

    private static ConsoleColor StatusColor(SnapshotStatus status) => status switch
    {
        SnapshotStatus.Completed => ConsoleColor.Green,
        SnapshotStatus.Partial => ConsoleColor.Yellow,
        SnapshotStatus.Failed or SnapshotStatus.Corrupted => ConsoleColor.Red,
        _ => ConsoleColor.Gray
    };

    private static string ProviderMarker(ProviderStatus status) => status switch
    {
        ProviderStatus.Success => "OK",
        ProviderStatus.Partial => "WARN",
        ProviderStatus.Failed => "FAIL",
        ProviderStatus.Skipped => "SKIP",
        ProviderStatus.Cancelled => "STOP",
        _ => "?"
    };

    private static string DiagnosticMarker(TerminalDiagnosticState state) => state switch
    {
        TerminalDiagnosticState.Ok => "OK",
        TerminalDiagnosticState.Warning => "WARN",
        TerminalDiagnosticState.Error => "FAIL",
        _ => "INFO"
    };

    private char SpinnerFrame()
    {
        const string frames = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";
        char value = frames[_spinnerIndex % frames.Length];
        _spinnerIndex++;
        return value;
    }

    private static void WriteTransientLine(string text, ConsoleColor color, int width)
    {
        SetColor(color);
        Console.Write('\r');
        Console.Write(Fit(text, width));
        Console.ResetColor();
    }

    private static void WriteTopBorder(int width, ConsoleColor color)
    {
        SetColor(color);
        Console.WriteLine($"╭{new string('─', Math.Max(0, width - 2))}╮");
        Console.ResetColor();
    }

    private static void WriteBottomBorder(int width, ConsoleColor color)
    {
        SetColor(color);
        Console.WriteLine($"╰{new string('─', Math.Max(0, width - 2))}╯");
        Console.ResetColor();
    }

    private static void WriteSeparator(int width, ConsoleColor color)
    {
        SetColor(color);
        Console.WriteLine($"├{new string('─', Math.Max(0, width - 2))}┤");
        Console.ResetColor();
    }

    private static void WriteSingleLine(
        string text,
        int width,
        ConsoleColor color,
        bool centered = false,
        ConsoleColor? background = null)
    {
        int innerWidth = Math.Max(1, width - 2);
        string content = centered
            ? Center(FitRaw(text, innerWidth), innerWidth)
            : Fit(text, innerWidth);
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
            WriteSingleLine($" {line}", width, color);
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

    private static IEnumerable<string> Wrap(string value, int width)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield return string.Empty;
            yield break;
        }

        string remaining = value.Replace("\r", string.Empty, StringComparison.Ordinal);
        foreach (string paragraph in remaining.Split('\n'))
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
        if (value.Length >= width)
        {
            return value;
        }

        int left = (width - value.Length) / 2;
        return new string(' ', left) + value + new string(' ', width - left - value.Length);
    }

    private static string Fit(string? value, int width)
    {
        string text = FitRaw(value ?? string.Empty, width);
        return text.PadRight(Math.Max(0, width));
    }

    private static string FitRaw(string value, int width)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        string sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        return sanitized.Length <= width
            ? sanitized
            : width == 1
                ? "…"
                : sanitized[..(width - 1)] + "…";
    }

    private static void SetColor(ConsoleColor foreground, ConsoleColor? background = null)
    {
        try
        {
            Console.ForegroundColor = foreground;
            if (background is not null)
            {
                Console.BackgroundColor = background.Value;
            }
            else
            {
                Console.BackgroundColor = ConsoleColor.Black;
            }
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
            return new ConsoleKeyInfo('q', ConsoleKey.Q, shift: false, alt: false, control: false);
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
