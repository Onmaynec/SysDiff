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
            int width = Math.Min(140, TerminalCapabilities.GetSafeWindowWidth());
            int rows = Math.Max(5, TerminalCapabilities.GetSafeWindowHeight() - 15);
            StartPanel("COMPARISON LAB · CHANGE EXPLORER", width, ConsoleColor.Cyan);
            WriteLine(
                $"Найдено {changes.Count:N0} · min {minimumSeverity} · sort {(severitySort ? "severity" : "provider")} · raw {(rawMode ? "on" : "off")} · search {query}",
                width,
                ConsoleColor.DarkGray);
            Separator(width);

            if (changes.Count == 0)
            {
                WriteLine("Нет изменений для текущего фильтра.", width, ConsoleColor.Yellow, centered: true);
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
                        $"{(selected ? '▶' : ' ')} [{change.Severity,-8}] {change.ChangeType,-9} {change.DisplayName}",
                        width,
                        selected ? ConsoleColor.Black : SeverityColor(change.Severity),
                        background: selected ? ConsoleColor.Cyan : null);
                }

                SystemChange active = changes[Math.Clamp(selectedIndex, 0, changes.Count - 1)];
                Separator(width);
                WriteWrapped(active.Explanation, width, SeverityColor(active.Severity));
                WriteWrapped(active.WhyThisMatters, width, ConsoleColor.Gray);
                foreach (PropertyChange property in active.ChangedProperties.Take(3))
                {
                    WriteWrapped(
                        $"{property.Name}: {property.Before?.Value ?? "∅"} → {property.After?.Value ?? "∅"}",
                        width,
                        ConsoleColor.DarkGray);
                }
            }

            Separator(width);
            WriteLine(
                "↑↓ выбор  / поиск  F severity  S сортировка  R raw  E экспорт  Esc назад",
                width,
                ConsoleColor.DarkGray,
                centered: true);
            EndPanel(width);
        }
    }

    public static ConsoleColor SeverityColor(Severity severity) => severity switch
    {
        Severity.Critical or Severity.High => ConsoleColor.Red,
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
}
