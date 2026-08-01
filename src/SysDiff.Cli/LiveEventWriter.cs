using System.Text;
using System.Text.Json;
using SysDiff.Domain;

namespace SysDiff.Cli;

internal static class LiveEventWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string Render(IReadOnlyCollection<LiveEvent> events, string format) =>
        format.ToLowerInvariant() switch
        {
            "json" => JsonSerializer.Serialize(events, JsonOptions),
            "markdown" or "md" => RenderMarkdown(events),
            _ => throw new ArgumentException("Формат live-экспорта должен быть json или markdown.")
        };

    private static string RenderMarkdown(IEnumerable<LiveEvent> events)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# SysDiff live events");
        builder.AppendLine();
        builder.AppendLine("| UTC | Категория | Событие | Объект |");
        builder.AppendLine("|---|---|---|---|");
        foreach (LiveEvent item in events)
        {
            builder.Append('|')
                .Append(item.TimestampUtc.ToString("O"))
                .Append('|')
                .Append(Escape(item.Category))
                .Append('|')
                .Append(Escape(item.EventType))
                .Append('|')
                .Append(Escape(item.DisplayName))
                .AppendLine("|");

            if (item.Properties.Count > 0)
            {
                builder.AppendLine();
                foreach ((string name, string? value) in item.Properties)
                {
                    builder.Append("- **")
                        .Append(Escape(name))
                        .Append(":** `")
                        .Append(Escape(value ?? string.Empty))
                        .AppendLine("`");
                }

                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("`", "'", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
