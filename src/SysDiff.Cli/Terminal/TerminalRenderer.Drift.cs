using SysDiff.Domain;

namespace SysDiff.Cli;

internal sealed partial class TerminalRenderer
{
    public void RenderDriftSummary(DriftScanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Clear();
        int width = Math.Min(126, TerminalCapabilities.GetSafeWindowWidth());
        ConsoleColor riskColor = DriftColor(result.Risk.Level);
        StartPanel("DRIFT OPERATIONS // SCAN RESULT", width, riskColor);
        WriteKeyValue("Baseline", result.Baseline.SnapshotName, width, CyberTheme.Success);
        WriteKeyValue("Current", result.CurrentSnapshot.Name, width, CyberTheme.Secondary);
        WriteKeyValue("Snapshot status", result.CurrentSnapshot.Status.ToString(), width,
            result.CurrentSnapshot.Status == SnapshotStatus.Completed ? CyberTheme.Success : CyberTheme.Warning);
        WriteKeyValue("Comparison ID", result.Comparison.Id.ToString("D"), width);
        Separator(width, riskColor);
        WriteLine(
            $"RISK CHANNEL // {result.Risk.Score:000}/100 // {result.Risk.Level.ToString().ToUpperInvariant()}",
            width,
            riskColor,
            centered: true);
        WriteLine(
            $"[{CyberAnimation.BuildProgressBar(result.Risk.Score / 100d, Math.Max(12, width - 26))}]",
            width,
            riskColor,
            centered: true);
        WriteKeyValue("Changes", result.Risk.TotalChanges.ToString("N0"), width, riskColor);
        foreach (Severity severity in Enum.GetValues<Severity>().OrderByDescending(value => value))
        {
            int count = result.Risk.SeverityCounts.GetValueOrDefault(severity);
            if (count > 0)
            {
                WriteKeyValue($"Severity {severity}", count.ToString("N0"), width, SeverityColor(severity));
            }
        }
        Separator(width, riskColor);
        foreach (string factor in result.Risk.Factors)
        {
            WriteWrapped($"{CyberTheme.StageMarker(CyberStageState.Running)} {factor}", width, CyberTheme.Text);
        }
        Separator(width, riskColor);
        WriteWrapped($"HTML > {result.HtmlReportPath}", width, CyberTheme.Muted);
        WriteWrapped($"JSON > {result.JsonReportPath}", width, CyberTheme.Muted);
        WriteLine("Нажмите любую клавишу для возврата в Drift Operations", width, CyberTheme.Muted, centered: true);
        EndPanel(width, riskColor);
        ReadKey();
    }

    private static ConsoleColor DriftColor(DriftLevel level) => level switch
    {
        DriftLevel.Stable => CyberTheme.Success,
        DriftLevel.Notice => CyberTheme.Secondary,
        DriftLevel.Elevated => CyberTheme.Warning,
        DriftLevel.High or DriftLevel.Critical => CyberTheme.Error,
        _ => CyberTheme.Text
    };
}
