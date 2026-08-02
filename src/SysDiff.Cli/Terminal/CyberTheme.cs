using SysDiff.Domain;

namespace SysDiff.Cli;

public enum CyberStageState
{
    Queued,
    Running,
    Completed,
    Warning,
    Failed,
    Cancelled
}

public static class CyberTheme
{
    public const string Version = SysDiffProduct.Version;
    public const string ProductTitle = "SYSDIFF CYBER CONSOLE";
    public const string ProductSubtitle = "WINDOWS SCHEMA, MIGRATION & DRIFT CONTROL NODE";

    public static ConsoleColor Accent => ConsoleColor.Green;

    public static ConsoleColor Secondary => ConsoleColor.Cyan;

    public static ConsoleColor Muted => ConsoleColor.DarkGray;

    public static ConsoleColor Text => ConsoleColor.Gray;

    public static ConsoleColor Success => ConsoleColor.Green;

    public static ConsoleColor Warning => ConsoleColor.Yellow;

    public static ConsoleColor Error => ConsoleColor.Red;

    public static ConsoleColor Border => ConsoleColor.DarkGreen;

    public static ConsoleColor SelectionForeground => ConsoleColor.Black;

    public static ConsoleColor SelectionBackground => ConsoleColor.Green;

    public static string StageMarker(CyberStageState state) => state switch
    {
        CyberStageState.Queued => "[--]",
        CyberStageState.Running => "[>>]",
        CyberStageState.Completed => "[OK]",
        CyberStageState.Warning => "[!!]",
        CyberStageState.Failed => "[XX]",
        CyberStageState.Cancelled => "[//]",
        _ => "[??]"
    };

    public static ConsoleColor StageColor(CyberStageState state) => state switch
    {
        CyberStageState.Running => Secondary,
        CyberStageState.Completed => Success,
        CyberStageState.Warning => Warning,
        CyberStageState.Failed => Error,
        CyberStageState.Cancelled => Warning,
        _ => Muted
    };

    public static string NodeBadge(bool online) => online ? "[ NODE:ONLINE ]" : "[ NODE:DEGRADED ]";

    public static string PrivilegeBadge(bool administrator) =>
        administrator ? "[ ROOT:ADMIN ]" : "[ ROOT:USER ]";
}
