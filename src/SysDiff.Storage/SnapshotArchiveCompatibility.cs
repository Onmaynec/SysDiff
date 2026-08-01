namespace SysDiff.Storage;

public enum SnapshotArchiveCompatibilityStatus
{
    Compatible,
    RequiresNewerSysDiff,
    UnsupportedLegacy,
    Invalid
}

public sealed record SnapshotArchiveInspection
{
    public required string ArchivePath { get; init; }

    public SnapshotArchiveCompatibilityStatus Status { get; init; }

    public string? Format { get; init; }

    public int? FormatVersion { get; init; }

    public int? SchemaVersion { get; init; }

    public string? ProducerVersion { get; init; }

    public Guid? SnapshotId { get; init; }

    public DateTimeOffset? CreatedAtUtc { get; init; }

    public bool ChecksumsValid { get; init; }

    public bool CanImport { get; init; }

    public string Message { get; init; } = string.Empty;

    public List<string> Warnings { get; init; } = [];
}

public static class SnapshotArchiveCompatibility
{
    public const string FormatName = "SysDiff Snapshot";
    public const int CurrentFormatVersion = 1;
    public const int MinimumReadableFormatVersion = 1;
    public const int CurrentSchemaVersion = 1;
    public const int MinimumReadableSchemaVersion = 1;

    public static SnapshotArchiveInspection Evaluate(
        string archivePath,
        string? format,
        int formatVersion,
        int schemaVersion,
        string? producerVersion,
        Guid manifestSnapshotId,
        Guid snapshotId,
        int snapshotSchemaVersion,
        string? snapshotProducerVersion,
        DateTimeOffset createdAtUtc)
    {
        var warnings = new List<string>();

        if (!string.Equals(format, FormatName, StringComparison.Ordinal))
        {
            return Invalid(archivePath, "Manifest содержит неизвестный идентификатор формата.");
        }

        if (manifestSnapshotId == Guid.Empty || snapshotId == Guid.Empty || manifestSnapshotId != snapshotId)
        {
            return Invalid(archivePath, "Snapshot ID в manifest.json и snapshot.json не совпадает.");
        }

        if (schemaVersion != snapshotSchemaVersion)
        {
            return Invalid(archivePath, "Версия схемы в manifest.json и snapshot.json не совпадает.");
        }

        if (!string.Equals(producerVersion, snapshotProducerVersion, StringComparison.Ordinal))
        {
            warnings.Add("Версия SysDiff в manifest.json отличается от snapshot.json.");
        }

        if (formatVersion > CurrentFormatVersion || schemaVersion > CurrentSchemaVersion)
        {
            return new SnapshotArchiveInspection
            {
                ArchivePath = archivePath,
                Status = SnapshotArchiveCompatibilityStatus.RequiresNewerSysDiff,
                Format = format,
                FormatVersion = formatVersion,
                SchemaVersion = schemaVersion,
                ProducerVersion = producerVersion,
                SnapshotId = snapshotId,
                CreatedAtUtc = createdAtUtc,
                ChecksumsValid = true,
                CanImport = false,
                Message = "Архив создан более новой версией формата. Обновите SysDiff перед импортом.",
                Warnings = warnings
            };
        }

        if (formatVersion < MinimumReadableFormatVersion
            || schemaVersion < MinimumReadableSchemaVersion)
        {
            return new SnapshotArchiveInspection
            {
                ArchivePath = archivePath,
                Status = SnapshotArchiveCompatibilityStatus.UnsupportedLegacy,
                Format = format,
                FormatVersion = formatVersion,
                SchemaVersion = schemaVersion,
                ProducerVersion = producerVersion,
                SnapshotId = snapshotId,
                CreatedAtUtc = createdAtUtc,
                ChecksumsValid = true,
                CanImport = false,
                Message = "Архив использует устаревшую схему без безопасного migration path.",
                Warnings = warnings
            };
        }

        return new SnapshotArchiveInspection
        {
            ArchivePath = archivePath,
            Status = SnapshotArchiveCompatibilityStatus.Compatible,
            Format = format,
            FormatVersion = formatVersion,
            SchemaVersion = schemaVersion,
            ProducerVersion = producerVersion,
            SnapshotId = snapshotId,
            CreatedAtUtc = createdAtUtc,
            ChecksumsValid = true,
            CanImport = true,
            Message = "Архив совместим и может быть импортирован без миграции.",
            Warnings = warnings
        };
    }

    public static SnapshotArchiveInspection Invalid(string archivePath, string message) =>
        new()
        {
            ArchivePath = archivePath,
            Status = SnapshotArchiveCompatibilityStatus.Invalid,
            ChecksumsValid = false,
            CanImport = false,
            Message = message
        };
}
