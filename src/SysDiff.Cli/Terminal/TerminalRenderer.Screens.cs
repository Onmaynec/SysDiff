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
            int width = Math.Min(140, TerminalCapabilities.GetSafeWindowWidth());
            bool compact = width < 96;
            TopBorder(width, CyberTheme.Border);
            if (compact)
            {
                WriteLine(CyberTheme.ProductTitle, width, CyberTheme.Accent, centered: true);
                WriteLine($"{CyberTheme.ProductSubtitle} // v{CyberTheme.Version}", width, CyberTheme.Secondary, centered: true);
            }
            else
            {
                foreach (string line in Logo)
                {
                    WriteLine(line, width, CyberTheme.Accent, centered: true);
                }
                WriteLine($"{CyberTheme.ProductSubtitle} // BUILD {CyberTheme.Version}", width, CyberTheme.Secondary, centered: true);
            }

            Separator(width);
            WriteLine(
                $"{CyberTheme.NodeBadge(true)} {CyberTheme.PrivilegeBadge(state.IsAdministrator)} [ OS:{state.WindowsVersion} ] [ ARCH:{(Environment.Is64BitOperatingSystem ? "X64" : "X86")} ]",
                width,
                state.IsAdministrator ? CyberTheme.Success : CyberTheme.Warning);
            WriteLine(
                $"[ SNAPSHOTS:{state.SnapshotCount:N0} ] [ REPORTS:{state.ReportCount:N0} ] [ PROVIDERS:{state.ProviderCount:N0} ] [ STORAGE:{(state.PortableMode ? "PORTABLE" : "PROFILE")} ]",
                width,
                CyberTheme.Secondary);
            WriteLine($"DATA CHANNEL > {state.DataDirectory}", width, CyberTheme.Muted);
            Separator(width);
            WriteLine("COMMAND DECK // SELECT MODULE", width, CyberTheme.Accent, centered: true);

            foreach ((TerminalMenuItem item, int index) in menu.Select((value, index) => (value, index)))
            {
                bool selected = index == selectedIndex;
                string number = (index + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
                string status = selected ? "ARMED" : "READY";
                WriteLine(
                    $"{(selected ? '▶' : ' ')} [{number}] {item.Glyph} {item.Title,-22} // {item.Description,-48} [{status}]",
                    width,
                    selected ? CyberTheme.SelectionForeground : CyberTheme.Text,
                    background: selected ? CyberTheme.SelectionBackground : null);
            }

            Separator(width);
            WriteLine(
                "QUICK OPS > 1-9 MODULES · P/B/A SNAPSHOT · C DIFF · W WATCH · L LIVE · D DIAG",
                width,
                CyberTheme.Secondary,
                centered: true);
            WriteLine(
                "NAV > ↑↓ MOVE · ENTER EXECUTE · ESC BACK · F5 RESCAN · Q DISCONNECT",
                width,
                CyberTheme.Muted,
                centered: true);
            EndPanel(width);
        }
    }

    private void RenderBootFrame(
        IReadOnlyList<string> steps,
        int activeIndex,
        int frame)
    {
        lock (_consoleLock)
        {
            Clear();
            int width = Math.Min(116, TerminalCapabilities.GetSafeWindowWidth());
            TopBorder(width, CyberTheme.Border);
            foreach (string line in Logo)
            {
                WriteLine(line, width, CyberTheme.Accent, centered: true);
            }
            WriteLine("CYBER CONSOLE BOOTSTRAP // SECURE LOCAL SESSION", width, CyberTheme.Secondary, centered: true);
            Separator(width);
            for (int index = 0; index < steps.Count; index++)
            {
                CyberStageState state = index < activeIndex
                    ? CyberStageState.Completed
                    : index == activeIndex
                        ? CyberStageState.Running
                        : CyberStageState.Queued;
                WriteLine(
                    $"{CyberTheme.StageMarker(state)} {steps[index]}",
                    width,
                    CyberTheme.StageColor(state));
            }
            Separator(width);
            WriteLine(
                $"SIGNAL {CyberAnimation.BuildScanner((activeIndex * 3) + frame, Math.Max(20, width - 12))}",
                width,
                CyberTheme.Secondary);
            WriteLine(
                $"BOOT VECTOR {activeIndex + 1}/{steps.Count} {CyberAnimation.Pulse(frame)}  PRESS ANY KEY TO SKIP",
                width,
                CyberTheme.Muted,
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
            int width = Math.Min(118, TerminalCapabilities.GetSafeWindowWidth());
            int rows = Math.Max(5, TerminalCapabilities.GetSafeWindowHeight() - 13);
            StartPanel($"COMMAND DECK // {title}", width, CyberTheme.Accent);
            WriteLine("[ INPUT:ARROWS ] [ EXECUTE:ENTER ] [ CHANNEL:LOCAL ]", width, CyberTheme.Muted);
            WriteWrapped(subtitle, width, CyberTheme.Text);
            Separator(width);

            int start = Math.Max(0, selectedIndex - rows / 2);
            int end = Math.Min(items.Count, start + rows);
            start = Math.Max(0, end - rows);
            for (int index = start; index < end; index++)
            {
                bool selected = index == selectedIndex;
                WriteLine(
                    $"{(selected ? '▶' : ' ')} [{index + 1:00}] {label(items[index])}",
                    width,
                    selected ? CyberTheme.SelectionForeground : CyberTheme.Text,
                    background: selected ? CyberTheme.SelectionBackground : null);
            }

            Separator(width);
            WriteLine(
                allowBack
                    ? "↑↓ SELECT · ENTER EXECUTE · 1-9 DIRECT · ESC RETURN"
                    : "↑↓ SELECT · ENTER AUTHORIZE",
                width,
                CyberTheme.Muted,
                centered: true);
            EndPanel(width);
        }
    }

    public void RenderSnapshotDetails(SnapshotRecord snapshot)
    {
        Clear();
        int width = Math.Min(126, TerminalCapabilities.GetSafeWindowWidth());
        StartPanel($"SNAPSHOT NODE // {snapshot.Name}", width, CyberTheme.Accent);
        WriteLine("[ DATA:LOCAL ] [ SCHEMA:1 ] [ INTEGRITY:TRACKED ]", width, CyberTheme.Muted);
        WriteKeyValue("SNAPSHOT ID", snapshot.Id.ToString(), width);
        WriteKeyValue("CAPTURED", snapshot.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"), width);
        WriteKeyValue("PROFILE", snapshot.ProfileName.ToUpperInvariant(), width);
        WriteKeyValue("STATUS", snapshot.Status.ToString().ToUpperInvariant(), width, SnapshotStatusColor(snapshot.Status));
        WriteKeyValue("ARTIFACTS", snapshot.Artifacts.Count.ToString("N0"), width, CyberTheme.Secondary);
        Separator(width);
        foreach (ProviderSnapshotResult result in snapshot.ProviderResults.OrderBy(value => value.DisplayName))
        {
            string marker = result.Status switch
            {
                ProviderStatus.Success => "[OK]",
                ProviderStatus.Partial => "[!!]",
                ProviderStatus.Failed => "[XX]",
                _ => "[--]"
            };
            WriteLine(
                $"{marker} {result.DisplayName,-32} {result.ArtifactCount,12:N0} OBJECTS · {result.Duration.TotalSeconds,7:0.0}s",
                width,
                ProviderStatusColor(result.Status));
        }
        Separator(width);
        WriteLine("PRESS ANY KEY TO RETURN", width, CyberTheme.Muted, centered: true);
        EndPanel(width);
        ReadKey();
    }

    public void RenderComparisonSummary(
        SnapshotRecord before,
        SnapshotRecord after,
        ComparisonResult comparison)
    {
        Clear();
        int width = Math.Min(126, TerminalCapabilities.GetSafeWindowWidth());
        StartPanel("DIFF LAB // ANALYSIS OVERVIEW", width, CyberTheme.Accent);
        WriteLine("[ ENGINE:ONLINE ] [ SEVERITY:ACTIVE ] [ NOISE:FILTERED ]", width, CyberTheme.Muted);
        WriteKeyValue("BEFORE", before.Name, width);
        WriteKeyValue("AFTER", after.Name, width);
        WriteKeyValue("NOISE MODE", comparison.NoiseMode.ToString().ToUpperInvariant(), width);
        WriteKeyValue("CROSS NODE", comparison.CrossMachine ? "ENABLED" : "DISABLED", width, comparison.CrossMachine ? CyberTheme.Warning : CyberTheme.Success);
        Separator(width);
        foreach (ChangeType type in Enum.GetValues<ChangeType>())
        {
            int count = comparison.Changes.Count(change => change.ChangeType == type);
            if (count > 0)
            {
                WriteKeyValue(type.ToString().ToUpperInvariant(), count.ToString("N0"), width, ChangeColor(type));
            }
        }
        Separator(width);
        foreach (Severity severity in Enum.GetValues<Severity>().OrderByDescending(value => value))
        {
            int count = comparison.Changes.Count(change => change.Severity == severity);
            if (count > 0)
            {
                WriteKeyValue($"THREAT {severity.ToString().ToUpperInvariant()}", count.ToString("N0"), width, SeverityColor(severity));
            }
        }
        WriteKeyValue("NOISE HIDDEN", comparison.HiddenAsNoise.ToString("N0"), width, CyberTheme.Muted);
        foreach (string warning in comparison.Warnings)
        {
            WriteWrapped($"[!!] {warning}", width, CyberTheme.Warning);
        }
        Separator(width);
        WriteLine("PRESS ANY KEY TO OPEN COMMAND DECK", width, CyberTheme.Muted, centered: true);
        EndPanel(width);
        ReadKey();
    }

    public void RenderLiveEvents(string title, IReadOnlyList<LiveEvent> events)
    {
        Clear();
        int width = Math.Min(136, TerminalCapabilities.GetSafeWindowWidth());
        StartPanel($"SIGNAL MONITOR // {title}", width, CyberTheme.Accent);
        WriteLine("[ CAPTURE:METADATA ONLY ] [ PAYLOAD:NOT READ ] [ MODE:PASSIVE ]", width, CyberTheme.Muted);
        WriteKeyValue("EVENTS", events.Count.ToString("N0"), width, CyberTheme.Secondary);
        foreach (IGrouping<string, LiveEvent> group in events.GroupBy(value => value.EventType).OrderBy(value => value.Key))
        {
            WriteKeyValue(group.Key.ToUpperInvariant(), group.Count().ToString("N0"), width, CyberTheme.Accent);
        }
        Separator(width);
        foreach (LiveEvent item in events.TakeLast(14))
        {
            string marker = item.EventType is "Started" or "Opened" ? "+" : "-";
            WriteLine(
                $"[{marker}] {item.TimestampUtc.ToLocalTime():HH:mm:ss.fff}  {item.EventType,-8}  {item.DisplayName}",
                width,
                item.EventType is "Started" or "Opened" ? CyberTheme.Success : CyberTheme.Warning);
        }
        Separator(width);
        WriteLine("PRESS ANY KEY TO RETURN", width, CyberTheme.Muted, centered: true);
        EndPanel(width);
        ReadKey();
    }

    public void RenderDiagnostics(IReadOnlyList<TerminalDiagnosticItem> diagnostics)
    {
        Clear();
        int width = Math.Min(122, TerminalCapabilities.GetSafeWindowWidth());
        StartPanel("SYSTEM DIAGNOSTICS // NODE HEALTH", width, CyberTheme.Accent);
        WriteLine("[ SELF TEST:ACTIVE ] [ STORAGE:PROBED ] [ PROVIDERS:INDEXED ]", width, CyberTheme.Muted);
        foreach (TerminalDiagnosticItem item in diagnostics)
        {
            ConsoleColor color = item.State switch
            {
                TerminalDiagnosticState.Ok => CyberTheme.Success,
                TerminalDiagnosticState.Warning => CyberTheme.Warning,
                TerminalDiagnosticState.Error => CyberTheme.Error,
                _ => CyberTheme.Text
            };
            string marker = item.State switch
            {
                TerminalDiagnosticState.Ok => "[OK]",
                TerminalDiagnosticState.Warning => "[!!]",
                TerminalDiagnosticState.Error => "[XX]",
                _ => "[--]"
            };
            WriteLine($"{marker} {item.Name,-24} {item.Value}", width, color);
        }
        Separator(width);
        WriteLine("PRESS ANY KEY TO RETURN", width, CyberTheme.Muted, centered: true);
        EndPanel(width);
        ReadKey();
    }

    private static ConsoleColor SnapshotStatusColor(SnapshotStatus status) => status switch
    {
        SnapshotStatus.Completed => CyberTheme.Success,
        SnapshotStatus.Partial => CyberTheme.Warning,
        SnapshotStatus.Failed or SnapshotStatus.Corrupted => CyberTheme.Error,
        _ => CyberTheme.Text
    };

    private static ConsoleColor ProviderStatusColor(ProviderStatus status) => status switch
    {
        ProviderStatus.Success => CyberTheme.Success,
        ProviderStatus.Partial => CyberTheme.Warning,
        ProviderStatus.Failed => CyberTheme.Error,
        _ => CyberTheme.Muted
    };
}
