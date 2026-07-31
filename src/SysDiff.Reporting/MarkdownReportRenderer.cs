using System.Text;
using SysDiff.Domain;

namespace SysDiff.Reporting;

public sealed class MarkdownReportRenderer
{
    public string Render(
        SnapshotRecord before,
        SnapshotRecord after,
        ComparisonResult comparison)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Отчёт SysDiff: {Escape(before.Name)} → {Escape(after.Name)}");
        builder.AppendLine();
        builder.AppendLine($"- **Создан:** {comparison.CreatedAtUtc:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"- **Режим фильтрации:** {comparison.NoiseMode}");
        builder.AppendLine($"- **Показано изменений:** {comparison.Changes.Count}");
        builder.AppendLine($"- **Скрыто как шум:** {comparison.HiddenAsNoise}");
        builder.AppendLine();

        foreach (IGrouping<string, SystemChange> group in comparison.Changes
                     .GroupBy(x => x.ProviderId)
                     .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"## {Escape(group.Key)}");
            builder.AppendLine();

            foreach (SystemChange change in group)
            {
                builder.AppendLine(
                    $"### {change.ChangeType} · {change.Severity} · {Escape(change.DisplayName)}");
                builder.AppendLine();
                builder.AppendLine(Escape(change.Explanation));
                builder.AppendLine();
                builder.AppendLine($"**Почему это важно:** {Escape(change.WhyThisMatters)}");
                builder.AppendLine();

                if (change.ChangedProperties.Count > 0)
                {
                    builder.AppendLine("| Свойство | До | После |");
                    builder.AppendLine("|---|---|---|");

                    foreach (PropertyChange property in change.ChangedProperties)
                    {
                        builder.AppendLine(
                            $"| {Escape(property.Name)} | {Escape(Format(property.Before))} | {Escape(Format(property.After))} |");
                    }

                    builder.AppendLine();
                }
            }
        }

        return builder.ToString();
    }

    private static string Format(ArtifactValue? value) =>
        value is null ? "∅" : value.Redacted ? "<redacted>" : value.Value ?? "null";

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
}
