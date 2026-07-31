using System.Text;
using SysDiff.Domain;

namespace SysDiff.Reporting;

public sealed class ConsoleReportRenderer
{
    public string Render(
        SnapshotRecord before,
        SnapshotRecord after,
        ComparisonResult comparison)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"Сравнение: {before.Name} → {after.Name}");
        builder.AppendLine($"Создано: {comparison.CreatedAtUtc:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"Режим шума: {comparison.NoiseMode}");
        builder.AppendLine();

        AppendCount(builder, "Добавлено", comparison.Changes.Count(x => x.ChangeType == ChangeType.Added));
        AppendCount(builder, "Удалено", comparison.Changes.Count(x => x.ChangeType == ChangeType.Removed));
        AppendCount(builder, "Изменено", comparison.Changes.Count(x => x.ChangeType == ChangeType.Modified));
        builder.AppendLine($"Скрыто как шум: {comparison.HiddenAsNoise}");
        builder.AppendLine();

        foreach (IGrouping<string, SystemChange> group in comparison.Changes
                     .GroupBy(x => x.ProviderId)
                     .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"[{group.Key.ToUpperInvariant()}]");

            foreach (SystemChange change in group)
            {
                string marker = change.ChangeType switch
                {
                    ChangeType.Added => "+",
                    ChangeType.Removed => "-",
                    ChangeType.Modified => "~",
                    _ => "?"
                };

                builder.AppendLine(
                    $" {marker} [{change.Severity.ToString().ToUpperInvariant()}] {change.DisplayName}");
                builder.AppendLine($"   {change.Explanation}");

                foreach (PropertyChange property in change.ChangedProperties.Take(8))
                {
                    builder.AppendLine(
                        $"   · {property.Name}: {Format(property.Before)} → {Format(property.After)}");
                }
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendCount(StringBuilder builder, string label, int value) =>
        builder.AppendLine($"{label,-10}: {value,6}");

    private static string Format(ArtifactValue? value)
    {
        if (value is null)
        {
            return "∅";
        }

        string text = value.Redacted ? "<redacted>" : value.Value ?? "null";
        return text.Length <= 180 ? text : text[..180] + "…";
    }
}
