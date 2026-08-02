using System.Text.Json;
using System.Text.Json.Serialization;
using SysDiff.Domain;

namespace SysDiff.Reporting;

public sealed class JsonReportRenderer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Render(
        SnapshotRecord before,
        SnapshotRecord after,
        ComparisonResult comparison) =>
        JsonSerializer.Serialize(
            new
            {
                format = "SysDiff Comparison Report",
                formatVersion = 1,
                schemaVersion = SysDiffProduct.PublicSchemaVersion,
                sysDiffVersion = SysDiffProduct.Version,
                generatedAtUtc = DateTimeOffset.UtcNow,
                before = new
                {
                    before.Id,
                    before.Name,
                    before.CreatedAtUtc,
                    before.ProfileName,
                    before.Status
                },
                after = new
                {
                    after.Id,
                    after.Name,
                    after.CreatedAtUtc,
                    after.ProfileName,
                    after.Status
                },
                comparison
            },
            Options);
}
