using SysDiff.Domain;

namespace SysDiff.Cli;

internal sealed partial class TerminalRenderer
{
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
            int width = Math.Min(142, TerminalCapabilities.GetSafeWindowWidth());
            int rows = Math.Max(5, TerminalCapabilities.GetSafeWindowHeight() - 17);
            StartPanel("DIFF LAB // CHANGE EXPLORER", width, CyberTheme.Accent);
            WriteLine(
                $"[ RESULTS:{changes.Count:N0} ] [ MIN:{minimumSeverity.ToString().ToUpperInvariant()} ] [ SORT:{(severitySort ? "SEVERITY" : "PROVIDER")} ] [ RAW:{(rawMode ? "ON" : "OFF")} ]",
                width,
                CyberTheme.Secondary);
            WriteLine($"SEARCH VECTOR > {(string.IsNullOrWhiteSpace(query) ? "<EMPTY>" : query)}", width, CyberTheme.Muted);
            Separator(width);

            if (changes.Count == 0)
            {
                WriteLine("[--] NO CHANGES MATCH CURRENT FILTER VECTOR", width, CyberTheme.Warning, centered: true);
            }
            else
            {
                int start = Math.Max(0, selectedIndex - rows / 2);
                int end = Math.Min(changes.Count, start + rows);
                start = Math.Max(0, end - rows);
                for (int index = start; index < end; index++)
                {
                    SystemChange change = changes[index];
                    bool selected = index == selectedIndex;
                    WriteLine(
                        $"{(selected ? '▶' : ' ')} [{change.Severity,-8}] {ChangeMarker(change.ChangeType)} {change.ChangeType,-9} {change.ProviderId,-18} {change.DisplayName}",
                        width,
                        selected ? CyberTheme.SelectionForeground : SeverityColor(change.Severity),
                        background: selected ? CyberTheme.SelectionBackground : null);
                }

                SystemChange active = changes[Math.Clamp(selectedIndex, 0, changes.Count - 1)];
                Separator(width);
                WriteLine(
                    $"ACTIVE VECTOR > {active.Identity} · CONFIDENCE {active.Confidence:P0}",
                    width,
                    CyberTheme.Secondary);
                WriteWrapped(active.Explanation, width, SeverityColor(active.Severity));
                WriteWrapped(active.WhyThisMatters, width, CyberTheme.Text);
                foreach (PropertyChange property in active.ChangedProperties.Take(3))
                {
                    WriteWrapped(
                        $"DELTA {property.Name}: {property.Before?.Value ?? "∅"} -> {property.After?.Value ?? "∅"}",
                        width,
                        CyberTheme.Muted);
                }
            }

            Separator(width);
            WriteLine(
                "↑↓ VECTOR · / SEARCH · F SEVERITY · S SORT · R RAW · E EXPORT · ESC RETURN",
                width,
                CyberTheme.Muted,
                centered: true);
            EndPanel(width);
        }
    }

    public static ConsoleColor SeverityColor(Severity severity) => severity switch
    {
        Severity.Critical or Severity.High => CyberTheme.Error,
        Severity.Medium => CyberTheme.Warning,
        Severity.Low => CyberTheme.Secondary,
        _ => CyberTheme.Muted
    };

    public static ConsoleColor ChangeColor(ChangeType type) => type switch
    {
        ChangeType.Added => CyberTheme.Success,
        ChangeType.Removed => CyberTheme.Error,
        ChangeType.Modified => ConsoleColor.Magenta,
        ChangeType.Moved or ChangeType.Renamed => CyberTheme.Secondary,
        _ => CyberTheme.Text
    };

    private static string ChangeMarker(ChangeType type) => type switch
    {
        ChangeType.Added => "[+]",
        ChangeType.Removed => "[-]",
        ChangeType.Modified => "[*]",
        ChangeType.Moved => "[>]",
        ChangeType.Renamed => "[~]",
        _ => "[?]"
    };
}
