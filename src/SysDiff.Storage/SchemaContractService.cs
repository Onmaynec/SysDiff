using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SysDiff.Storage;

public sealed class SchemaContractService
{
    private const long MaximumDocumentBytes = 1024L * 1024L * 1024L;
    private const string SemVerPattern =
        "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z.-]+)?(?:\\+[0-9A-Za-z.-]+)?$";

    private static readonly IReadOnlyList<SchemaContractDescriptor> Contracts =
    [
        new()
        {
            Kind = SchemaContractKind.Snapshot,
            Key = "snapshot",
            DisplayName = "SysDiff Snapshot",
            SchemaId = "https://schemas.sysdiff.dev/v1/snapshot.schema.json",
            FileName = "snapshot.schema.json"
        },
        new()
        {
            Kind = SchemaContractKind.ComparisonReport,
            Key = "comparison",
            DisplayName = "SysDiff Comparison Report",
            SchemaId = "https://schemas.sysdiff.dev/v1/comparison-report.schema.json",
            FileName = "comparison-report.schema.json"
        },
        new()
        {
            Kind = SchemaContractKind.InvestigationBundleManifest,
            Key = "bundle",
            DisplayName = "SysDiff Investigation Bundle Manifest",
            SchemaId = "https://schemas.sysdiff.dev/v1/investigation-bundle-manifest.schema.json",
            FileName = "investigation-bundle-manifest.schema.json"
        }
    ];

    private static readonly HashSet<string> SnapshotStatuses =
        ["InProgress", "Completed", "Partial", "Failed", "Cancelled", "Corrupted"];
    private static readonly HashSet<string> ProviderStatuses =
        ["Success", "Partial", "Failed", "Skipped", "Cancelled"];
    private static readonly HashSet<string> ChangeTypes =
        ["Added", "Removed", "Modified", "Unchanged", "Moved", "Renamed", "Unknown"];
    private static readonly HashSet<string> Severities =
        ["Info", "Low", "Medium", "High", "Critical"];
    private static readonly HashSet<string> NoiseModes =
        ["Raw", "Balanced", "Strict"];

    public IReadOnlyList<SchemaContractDescriptor> ListContracts() => Contracts;

    public SchemaContractDescriptor GetContract(SchemaContractKind kind) =>
        Contracts.Single(value => value.Kind == kind);

    public SchemaContractKind ParseKind(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().ToLowerInvariant() switch
        {
            "snapshot" or "sdshot" => SchemaContractKind.Snapshot,
            "comparison" or "report" or "comparison-report" => SchemaContractKind.ComparisonReport,
            "bundle" or "manifest" or "investigation-bundle" =>
                SchemaContractKind.InvestigationBundleManifest,
            _ => throw new ArgumentException(
                "Контракт schema: snapshot, comparison или bundle.",
                nameof(value))
        };
    }

    public string GetSchemaJson(SchemaContractKind kind)
    {
        SchemaContractDescriptor contract = GetContract(kind);
        Assembly assembly = typeof(SchemaContractService).Assembly;
        string resourceName = assembly.GetManifestResourceNames().SingleOrDefault(name =>
            name.EndsWith(contract.FileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Embedded schema resource не найден: {contract.FileName}");
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded schema resource недоступен: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public async Task<SchemaValidationResult> ValidateFileAsync(
        SchemaContractKind kind,
        string inputPath,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(inputPath);
        SchemaContractDescriptor contract = GetContract(kind);
        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            return Invalid(contract, fullPath, "file_not_found", "$", "JSON-файл не найден.");
        }
        if (info.Length > MaximumDocumentBytes)
        {
            return Invalid(
                contract,
                fullPath,
                "document_too_large",
                "$",
                "JSON-документ превышает допустимый размер 1 ГБ.");
        }

        try
        {
            await using FileStream stream = new(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);
            using JsonDocument document = await JsonDocument.ParseAsync(
                stream,
                JsonDocumentOptions,
                cancellationToken);
            return ValidateElement(kind, document.RootElement, fullPath);
        }
        catch (JsonException exception)
        {
            return Invalid(
                contract,
                fullPath,
                "invalid_json",
                "$",
                $"Некорректный JSON: {exception.Message}");
        }
    }

    public SchemaValidationResult ValidateJson(
        SchemaContractKind kind,
        string json,
        string inputPath = "<memory>")
    {
        SchemaContractDescriptor contract = GetContract(kind);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, JsonDocumentOptions);
            return ValidateElement(kind, document.RootElement, inputPath);
        }
        catch (JsonException exception)
        {
            return Invalid(
                contract,
                inputPath,
                "invalid_json",
                "$",
                $"Некорректный JSON: {exception.Message}");
        }
    }

    private SchemaValidationResult ValidateElement(
        SchemaContractKind kind,
        JsonElement root,
        string inputPath)
    {
        SchemaContractDescriptor contract = GetContract(kind);
        var issues = new List<SchemaValidationIssue>();
        string schemaProperty = kind == SchemaContractKind.Snapshot
            ? "SchemaVersion"
            : "schemaVersion";
        int? schemaVersion = ReadSchemaVersion(root, schemaProperty);

        if (schemaVersion > contract.SchemaVersion)
        {
            issues.Add(new SchemaValidationIssue
            {
                Path = "$." + schemaProperty,
                Code = "requires_newer_sysdiff",
                Message = $"Документ использует schema version {schemaVersion}, " +
                          $"а текущий reader поддерживает максимум {contract.SchemaVersion}."
            });
            return new SchemaValidationResult
            {
                Contract = contract,
                InputPath = inputPath,
                Status = SchemaValidationStatus.RequiresNewerSysDiff,
                DocumentSchemaVersion = schemaVersion,
                Issues = issues
            };
        }

        var validator = new ContractValidator(issues);
        switch (kind)
        {
            case SchemaContractKind.Snapshot:
                ValidateSnapshot(root, validator);
                break;
            case SchemaContractKind.ComparisonReport:
                ValidateComparisonReport(root, validator);
                break;
            case SchemaContractKind.InvestigationBundleManifest:
                ValidateBundleManifest(root, validator);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        return new SchemaValidationResult
        {
            Contract = contract,
            InputPath = inputPath,
            Status = issues.Count == 0
                ? SchemaValidationStatus.Valid
                : SchemaValidationStatus.Invalid,
            DocumentSchemaVersion = schemaVersion,
            Issues = issues,
            Warnings = contract.AllowsAdditionalProperties
                ? ["Неизвестные additive properties разрешены и сохраняют forward compatibility."]
                : []
        };
    }

    private static void ValidateSnapshot(JsonElement root, ContractValidator validator)
    {
        if (!validator.Object(root, "$"))
        {
            return;
        }
        validator.Guid(root, "Id", "$");
        validator.NonEmptyString(root, "Name", "$");
        validator.DateTime(root, "CreatedAtUtc", "$");
        validator.SemVer(root, "SysDiffVersion", "$");
        validator.Integer(root, "SchemaVersion", "$", expected: 1);
        validator.NonEmptyString(root, "ProfileName", "$");
        validator.Enum(root, "Status", "$", SnapshotStatuses);
        validator.NonEmptyString(root, "Architecture", "$");

        if (validator.Array(root, "ProviderResults", "$", out JsonElement providers))
        {
            int index = 0;
            foreach (JsonElement provider in providers.EnumerateArray())
            {
                ValidateProvider(provider, $"$.ProviderResults[{index}]", validator);
                index++;
            }
        }
        if (validator.Array(root, "Artifacts", "$", out JsonElement artifacts))
        {
            ValidateArtifacts(artifacts, "$.Artifacts", validator);
        }
    }

    private static void ValidateProvider(
        JsonElement provider,
        string path,
        ContractValidator validator)
    {
        if (!validator.Object(provider, path))
        {
            return;
        }
        validator.NonEmptyString(provider, "ProviderId", path);
        validator.NonEmptyString(provider, "DisplayName", path);
        validator.Enum(provider, "Status", path, ProviderStatuses);
        validator.DateTime(provider, "StartedAtUtc", path);
        validator.DateTime(provider, "FinishedAtUtc", path);
        validator.NonNegativeInteger(provider, "ArtifactCount", path);
        validator.StringArray(provider, "Warnings", path);
        validator.StringArray(provider, "Errors", path);
        validator.Boolean(provider, "RequiresAdministrator", path);
        if (validator.Array(provider, "Artifacts", path, out JsonElement artifacts))
        {
            ValidateArtifacts(artifacts, path + ".Artifacts", validator);
        }
    }

    private static void ValidateArtifacts(
        JsonElement artifacts,
        string path,
        ContractValidator validator)
    {
        int index = 0;
        foreach (JsonElement artifact in artifacts.EnumerateArray())
        {
            string itemPath = $"{path}[{index}]";
            if (validator.Object(artifact, itemPath))
            {
                validator.NonEmptyString(artifact, "ProviderId", itemPath);
                validator.NonEmptyString(artifact, "ArtifactType", itemPath);
                validator.NonEmptyString(artifact, "Identity", itemPath);
                validator.NonEmptyString(artifact, "DisplayName", itemPath);
                validator.Property(artifact, "Properties", JsonValueKind.Object, itemPath, out _);
                validator.StringArray(artifact, "Tags", itemPath);
            }
            index++;
        }
    }

    private static void ValidateComparisonReport(
        JsonElement root,
        ContractValidator validator)
    {
        if (!validator.Object(root, "$"))
        {
            return;
        }
        validator.ExactString(root, "format", "SysDiff Comparison Report", "$");
        validator.Integer(root, "formatVersion", "$", expected: 1);
        validator.Integer(root, "schemaVersion", "$", expected: 1);
        validator.SemVer(root, "sysDiffVersion", "$");
        validator.DateTime(root, "generatedAtUtc", "$");

        if (validator.Property(root, "before", JsonValueKind.Object, "$", out JsonElement before))
        {
            ValidateSnapshotSummary(before, "$.before", validator);
        }
        if (validator.Property(root, "after", JsonValueKind.Object, "$", out JsonElement after))
        {
            ValidateSnapshotSummary(after, "$.after", validator);
        }
        if (validator.Property(root, "comparison", JsonValueKind.Object, "$", out JsonElement comparison))
        {
            ValidateComparison(comparison, "$.comparison", validator);
        }
    }

    private static void ValidateSnapshotSummary(
        JsonElement summary,
        string path,
        ContractValidator validator)
    {
        validator.Guid(summary, "id", path);
        validator.NonEmptyString(summary, "name", path);
        validator.DateTime(summary, "createdAtUtc", path);
        validator.NonEmptyString(summary, "profileName", path);
        validator.Enum(summary, "status", path, SnapshotStatuses);
    }

    private static void ValidateComparison(
        JsonElement comparison,
        string path,
        ContractValidator validator)
    {
        validator.Guid(comparison, "id", path);
        validator.Guid(comparison, "beforeSnapshotId", path);
        validator.Guid(comparison, "afterSnapshotId", path);
        validator.DateTime(comparison, "createdAtUtc", path);
        validator.Enum(comparison, "noiseMode", path, NoiseModes);
        validator.Boolean(comparison, "crossMachine", path);
        validator.StringArray(comparison, "warnings", path);
        validator.NonNegativeInteger(comparison, "hiddenAsNoise", path);

        if (validator.Array(comparison, "changes", path, out JsonElement changes))
        {
            int index = 0;
            foreach (JsonElement change in changes.EnumerateArray())
            {
                ValidateChange(change, $"{path}.changes[{index}]", validator);
                index++;
            }
        }
    }

    private static void ValidateChange(
        JsonElement change,
        string path,
        ContractValidator validator)
    {
        if (!validator.Object(change, path))
        {
            return;
        }
        validator.Guid(change, "id", path);
        validator.Enum(change, "changeType", path, ChangeTypes);
        validator.NonEmptyString(change, "providerId", path);
        validator.NonEmptyString(change, "artifactType", path);
        validator.NonEmptyString(change, "identity", path);
        validator.NonEmptyString(change, "displayName", path);
        validator.Property(change, "changedProperties", JsonValueKind.Array, path, out _);
        validator.Enum(change, "severity", path, Severities);
        validator.Property(change, "explanation", JsonValueKind.String, path, out _);
        validator.Property(change, "whyThisMatters", JsonValueKind.String, path, out _);
        validator.StringArray(change, "tags", path);
        validator.Number(change, "confidence", path, minimum: 0, maximum: 1);
        validator.Boolean(change, "isNoise", path);
    }

    private static void ValidateBundleManifest(
        JsonElement root,
        ContractValidator validator)
    {
        if (!validator.Object(root, "$"))
        {
            return;
        }
        validator.ExactString(root, "format", "SysDiff Investigation Bundle", "$");
        validator.Integer(root, "formatVersion", "$", expected: 1);
        validator.Integer(root, "schemaVersion", "$", expected: 1);
        validator.SemVer(root, "sysDiffVersion", "$");
        validator.DateTime(root, "createdAtUtc", "$");
        validator.Guid(root, "comparisonId", "$");
        validator.Guid(root, "beforeSnapshotId", "$");
        validator.Guid(root, "afterSnapshotId", "$");
        validator.Boolean(root, "crossMachine", "$");
        validator.StringArray(root, "warnings", "$");

        if (validator.Property(root, "privacy", JsonValueKind.Object, "$", out JsonElement privacy))
        {
            validator.Boolean(privacy, "userProfilePathsRedacted", "$.privacy");
            validator.Boolean(privacy, "privateKeysIncluded", "$.privacy");
            validator.Boolean(privacy, "rawLogsIncluded", "$.privacy");
        }
    }

    private static int? ReadSchemaVersion(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out int version))
        {
            return null;
        }
        return version;
    }

    private static SchemaValidationResult Invalid(
        SchemaContractDescriptor contract,
        string inputPath,
        string code,
        string path,
        string message) =>
        new()
        {
            Contract = contract,
            InputPath = inputPath,
            Status = SchemaValidationStatus.Invalid,
            Issues =
            [
                new SchemaValidationIssue
                {
                    Path = path,
                    Code = code,
                    Message = message
                }
            ]
        };

    private sealed class ContractValidator
    {
        private readonly List<SchemaValidationIssue> _issues;

        public ContractValidator(List<SchemaValidationIssue> issues)
        {
            _issues = issues;
        }

        public bool Object(JsonElement value, string path)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                return true;
            }
            Add(path, "type", "Ожидался JSON object.");
            return false;
        }

        public bool Property(
            JsonElement parent,
            string name,
            JsonValueKind kind,
            string path,
            out JsonElement value)
        {
            if (!parent.TryGetProperty(name, out value))
            {
                Add(path + "." + name, "required", "Обязательное поле отсутствует.");
                return false;
            }
            if (value.ValueKind != kind)
            {
                Add(
                    path + "." + name,
                    "type",
                    $"Ожидался тип {kind}, получен {value.ValueKind}.");
                return false;
            }
            return true;
        }

        public bool Array(
            JsonElement parent,
            string name,
            string path,
            out JsonElement value) =>
            Property(parent, name, JsonValueKind.Array, path, out value);

        public void NonEmptyString(JsonElement parent, string name, string path)
        {
            if (Property(parent, name, JsonValueKind.String, path, out JsonElement value)
                && string.IsNullOrWhiteSpace(value.GetString()))
            {
                Add(path + "." + name, "min_length", "Строка не может быть пустой.");
            }
        }

        public void ExactString(
            JsonElement parent,
            string name,
            string expected,
            string path)
        {
            if (Property(parent, name, JsonValueKind.String, path, out JsonElement value)
                && !string.Equals(value.GetString(), expected, StringComparison.Ordinal))
            {
                Add(path + "." + name, "const", $"Ожидалось значение '{expected}'.");
            }
        }

        public void Guid(JsonElement parent, string name, string path)
        {
            if (Property(parent, name, JsonValueKind.String, path, out JsonElement value)
                && !System.Guid.TryParse(value.GetString(), out _))
            {
                Add(path + "." + name, "format", "Ожидался UUID.");
            }
        }

        public void DateTime(JsonElement parent, string name, string path)
        {
            if (Property(parent, name, JsonValueKind.String, path, out JsonElement value)
                && !DateTimeOffset.TryParse(
                    value.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _))
            {
                Add(path + "." + name, "format", "Ожидалась дата RFC 3339.");
            }
        }

        public void SemVer(JsonElement parent, string name, string path)
        {
            if (Property(parent, name, JsonValueKind.String, path, out JsonElement value)
                && !Regex.IsMatch(
                    value.GetString() ?? string.Empty,
                    SemVerPattern,
                    RegexOptions.CultureInvariant))
            {
                Add(path + "." + name, "semver", "Ожидалась версия SemVer X.Y.Z.");
            }
        }

        public void Integer(
            JsonElement parent,
            string name,
            string path,
            int expected)
        {
            if (!Property(parent, name, JsonValueKind.Number, path, out JsonElement value))
            {
                return;
            }
            if (!value.TryGetInt32(out int parsed))
            {
                Add(path + "." + name, "integer", "Ожидалось целое число.");
            }
            else if (parsed != expected)
            {
                Add(
                    path + "." + name,
                    "const",
                    $"Поддерживается только schema version {expected}.");
            }
        }

        public void NonNegativeInteger(JsonElement parent, string name, string path)
        {
            if (!Property(parent, name, JsonValueKind.Number, path, out JsonElement value))
            {
                return;
            }
            if (!value.TryGetInt32(out int parsed) || parsed < 0)
            {
                Add(path + "." + name, "minimum", "Ожидалось целое число >= 0.");
            }
        }

        public void Number(
            JsonElement parent,
            string name,
            string path,
            double minimum,
            double maximum)
        {
            if (!Property(parent, name, JsonValueKind.Number, path, out JsonElement value))
            {
                return;
            }
            if (!value.TryGetDouble(out double parsed) || parsed < minimum || parsed > maximum)
            {
                Add(
                    path + "." + name,
                    "range",
                    $"Ожидалось число в диапазоне {minimum}..{maximum}.");
            }
        }

        public void Boolean(JsonElement parent, string name, string path)
        {
            if (!parent.TryGetProperty(name, out JsonElement value))
            {
                Add(path + "." + name, "required", "Обязательное поле отсутствует.");
                return;
            }
            if (value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            {
                Add(path + "." + name, "type", "Ожидался boolean.");
            }
        }

        public void Enum(
            JsonElement parent,
            string name,
            string path,
            HashSet<string> allowed)
        {
            if (Property(parent, name, JsonValueKind.String, path, out JsonElement value)
                && !allowed.Contains(value.GetString() ?? string.Empty))
            {
                Add(
                    path + "." + name,
                    "enum",
                    $"Недопустимое значение. Разрешено: {string.Join(", ", allowed)}.");
            }
        }

        public void StringArray(JsonElement parent, string name, string path)
        {
            if (!Array(parent, name, path, out JsonElement array))
            {
                return;
            }
            int index = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    Add($"{path}.{name}[{index}]", "type", "Ожидалась строка.");
                }
                index++;
            }
        }

        private void Add(string path, string code, string message) =>
            _issues.Add(new SchemaValidationIssue
            {
                Path = path,
                Code = code,
                Message = message
            });
    }

    private static readonly JsonDocumentOptions JsonDocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 128
    };
}
