using SysDiff.Domain;

namespace SysDiff.Cli;

internal sealed partial class TerminalRenderer
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
            TopBorder(width, ConsoleColor.DarkCyan);
            if (compact)
            {
                WriteLine("SYSDIFF CONTROL CENTER", width, ConsoleColor.Cyan, centered: true);
                WriteLine("Windows investigation console · v0.4.0", width, ConsoleColor.DarkGray, centered: true);
            }
            else
            {
                foreach (string line in Logo)
                {
                    WriteLine(line, width, ConsoleColor.Cyan, centered: true);
                }
                WriteLine("TERMINAL CONTROL CENTER · VERSION 0.4.0", width, ConsoleColor.DarkCyan, centered: true);
            }
            Separator(width);

            WriteLine(
                $"Windows {state.WindowsVersion} · {(state.IsAdministrator ? "Administrator" : "Standard user")} · snapshots {state.SnapshotCount:N0} · reports {state.ReportCount:N0} · providers {state.ProviderCount:N0}",
                width,
                state.IsAdministrator ? ConsoleColor.Green : ConsoleColor.Yellow);
            WriteLine(
                $"Storage: {(state.PortableMode ? "Portable" : "User profile")} · {state.DataDirectory}",
                width,
                ConsoleColor.DarkGray);
            Separator(width);

            foreach ((TerminalMenuItem item, int index) in menu.Select((value, index) => (value, index)))
            {
                bool selected = index == selectedIndex;
                WriteLine(
                    $"{(selected ? '▶' : ' ')} {item.Glyph} {item.Title,-22} {item.Description}",
                    width,
                    selected ? ConsoleColor.Black : ConsoleColor.Gray,
                    background: selected ? ConsoleColor.Cyan : null);
            }

            Separator(width);
            WriteLine(
                "↑ ↓  навигация    Enter  открыть    Esc  назад    F5  обновить    Q  выход",
                width,
                ConsoleColor.DarkGray,
                centered: true);
            EndPanel(width);
        }
    }

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
            int rows = Math.Max(5, TerminalCapabilities.GetSafeWindowHeight() - 10);
            StartPanel(title, width, ConsoleColor.Cyan);
            WriteWrapped(subtitle, width, ConsoleColor.DarkGray);
            Separator(width);

            int start = Math.Max(0, selectedIndex - rows / 2);
            int end = Math.Min(items.Count, start + rows);
            start = Math.Max(0, end - rows);
            for (int index = start; index < end; index++)
            {
                bool selected = index == selectedIndex;
                WriteLine(
                    $"{(selected ? '▶' : ' ')}  {label(items[index])}",
                    width,
                    selected ? ConsoleColor.Black : ConsoleColor.Gray,
                    background: selected ? ConsoleColor.Cyan : null);
            }

            Separator(width);
            WriteLine(
                allowBack ? "↑ ↓  выбор    Enter  открыть    Esc  назад" : "↑ ↓  выбор    Enter  подтвердить",
                width,
                ConsoleColor.DarkGray,
                centered: true);
            EndPanel(width);
        }
    }

    public void RenderSnapshotDetails(SnapshotRecord snapshot)
    {
        Clear();
        int width = Math.Min(122, TerminalCapabilities.GetSafeWindowWidth());
        StartPanel($"SNAPSHOT · {snapshot.Name}", width, ConsoleColor.Cyan);
        WriteKeyValue("ID", snapshot.Id.ToString(), width);
        WriteKeyValue("Создан", snapshot.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"), width);
        WriteKeyValue("Профиль", snapshot.ProfileName, width);
        WriteKeyValue("Статус", snapshot.Status.ToString(), width, SnapshotStatusColor(snapshot.Status));
        WriteKeyValue("Объектов", snapshot.Artifacts.Count.ToString("N0"), width);
        Separator(width);
        foreach (ProviderSnapshotResult result in snapshot.ProviderResults.OrderBy(value => value.DisplayName))
        {
            WriteLine(
                $"[{result.Status,-9}] {result.DisplayName,-30} {result.ArtifactCount,12:N0} объектов",
                width,
                ProviderStatusColor(result.Status));
        }
        Separator(width);
        WriteLine("Нажмите любую клавишу", width, ConsoleColor.DarkGray, centered: true);
        EndPanel(width);
        ReadKey();
    }

    public void RenderComparisonSummary(
        SnapshotRecord before,
        SnapshotRecord after,
        ComparisonResult comparison)
    {
        Clear();
        int width = Math.Min(122, TerminalCapabilities.GetSafeWindowWidth());
        StartPanel("COMPARISON LAB · OVERVIEW", width, ConsoleColor.Cyan);
        WriteKeyValue("Before", before.Name, width);
        WriteKeyValue("After", after.Name, width);
        WriteKeyValue("Noise", comparison.NoiseMode.ToString(), width);
        WriteKeyValue("Cross-machine", comparison.CrossMachine ? "да" : "нет", width, comparison.CrossMachine ? ConsoleColor.Yellow : ConsoleColor.Green);
        Separator(width);
        foreach (ChangeType type in Enum.GetValues<ChangeType>())
        {
            int count = comparison.Changes.Count(change => change.ChangeType == type);
            if (count > 0)
            {
                WriteKeyValue(type.ToString(), count.ToString("N0"), width, ChangeColor(type));
            }
        }
        Separator(width);
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
        Separator(width);
        WriteLine("Нажмите любую клавишу", width, ConsoleColor.DarkGray, centered: true);
        EndPanel(width);
        ReadKey();
    }

    public void RenderLiveEvents(string title, IReadOnlyList<LiveEvent> events)
    {
        Clear();
        int width = Math.Min(132, TerminalCapabilities.GetSafeWindowWidth());
        StartPanel(title, width, ConsoleColor.Cyan);
        WriteKeyValue("Событий", events.Count.ToString("N0"), width);
        foreach (IGrouping<string, LiveEvent> group in events.GroupBy(value => value.EventType).OrderBy(value => value.Key))
        {
            WriteKeyValue(group.Key, group.Count().ToString("N0"), width, ConsoleColor.DarkCyan);
        }
        Separator(width);
        foreach (LiveEvent item in events.TakeLast(14))
        {
            WriteLine(
                $"{item.TimestampUtc.ToLocalTime():HH:mm:ss}  {item.EventType,-8}  {item.DisplayName}",
                width,
                item.EventType is "Started" or "Opened" ? ConsoleColor.Green : ConsoleColor.Yellow);
        }
        Separator(width);
        WriteLine("Нажмите любую клавишу", width, ConsoleColor.DarkGray, centered: true);
        EndPanel(width);
        ReadKey();
    }

    public void RenderDiagnostics(IReadOnlyList<TerminalDiagnosticItem> diagnostics)
    {
        Clear();
        int width = Math.Min(118, TerminalCapabilities.GetSafeWindowWidth());
        StartPanel("SYSDIFF DIAGNOSTICS", width, ConsoleColor.Cyan);
        foreach (TerminalDiagnosticItem item in diagnostics)
        {
            ConsoleColor color = item.State switch
            {
                TerminalDiagnosticState.Ok => ConsoleColor.Green,
                TerminalDiagnosticState.Warning => ConsoleColor.Yellow,
                TerminalDiagnosticState.Error => ConsoleColor.Red,
                _ => ConsoleColor.Gray
            };
            WriteLine($"[{item.State,-7}] {item.Name,-24} {item.Value}", width, color);
        }
        Separator(width);
        WriteLine("Нажмите любую клавишу", width, ConsoleColor.DarkGray, centered: true);
        EndPanel(width);
        ReadKey();
    }

    private static ConsoleColor SnapshotStatusColor(SnapshotStatus status) => status switch
    {
        SnapshotStatus.Completed => ConsoleColor.Green,
        SnapshotStatus.Partial => ConsoleColor.Yellow,
        SnapshotStatus.Failed or SnapshotStatus.Corrupted => ConsoleColor.Red,
        _ => ConsoleColor.Gray
    };

    private static ConsoleColor ProviderStatusColor(ProviderStatus status) => status switch
    {
        ProviderStatus.Success => ConsoleColor.Green,
        ProviderStatus.Partial => ConsoleColor.Yellow,
        ProviderStatus.Failed => ConsoleColor.Red,
        _ => ConsoleColor.DarkGray
    };
}
