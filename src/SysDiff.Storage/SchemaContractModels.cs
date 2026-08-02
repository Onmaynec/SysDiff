using SysDiff.Domain;

namespace SysDiff.Storage;

public enum SchemaContractKind
{
    Snapshot,
    ComparisonReport,
    InvestigationBundleManifest
}

public enum SchemaValidationStatus
{
    Valid,
    Invalid,
    RequiresNewerSysDiff
}

public sealed record SchemaContractDescriptor
{
    public required SchemaContractKind Kind { get; init; }

    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public required string SchemaId { get; init; }

    public required string FileName { get; init; }

    public int SchemaVersion { get; init; } = SysDiffProduct.PublicSchemaVersion;

    public string JsonSchemaDraft { get; init; } = "2020-12";

    public string Stability { get; init; } = "stable";

    public string MinimumReaderVersion { get; init; } = "0.10.0";

    public string CurrentWriterVersion { get; init; } = SysDiffProduct.Version;

    public bool AllowsAdditionalProperties { get; init; } = true;
}

public sealed record SchemaValidationIssue
{
    public required string Path { get; init; }

    public required string Code { get; init; }

    public required string Message { get; init; }
}

public sealed record SchemaValidationResult
{
    public required SchemaContractDescriptor Contract { get; init; }

    public required string InputPath { get; init; }

    public SchemaValidationStatus Status { get; init; }

    public int? DocumentSchemaVersion { get; init; }

    public List<SchemaValidationIssue> Issues { get; init; } = [];

    public List<string> Warnings { get; init; } = [];

    public bool IsValid => Status == SchemaValidationStatus.Valid;
}
