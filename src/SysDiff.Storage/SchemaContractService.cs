using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SysDiff.Storage;

public sealed class SchemaContractService
{
    private const long MaximumDocumentBytes = 1024L * 1024L * 1024L;

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
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 128
                },
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
            using JsonDocument document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 128
                });
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
        int? schemaVersion = kind switch
        {
            SchemaContractKind.Snapshot => ReadSchemaVersion(root, "SchemaVersion"),
            _ => ReadSchemaVersion(root, "schemaVersion")
        };

        if (schemaVersion > contract.SchemaVersion)
        {
            issues.Add(new SchemaValidationIssue
            {
                Path = kind == SchemaContractKind.Snapshot ? "$.SchemaVersion" : "$.schemaVersion",
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

        switch (kind)
        {
            case SchemaContractKind.Snapshot:
                ValidateSnapshot(root, issues);
                break;
            case SchemaContractKind.ComparisonReport:
                ValidateComparisonReport(root, issues);
                break;
            case SchemaContractKind.InvestigationBundleManifest:
                ValidateBundleManifest(root, issues);
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

    private static void ValidateSnapshot(JsonElement root, List<SchemaValidationIssue> issues)
    {
        if (!RequireObject(root, "$", issues))
        {
            return;
        }

        RequireGuid(root, "Id", "$", issues);
        RequireNonEmptyString(root, "Name", "$", issues);
        RequireDateTime(root, "CreatedAtUtc", "$", issues);
        RequireVersionString(root, "SysDiffVersion", "$", issues);
        RequireInteger(root, "SchemaVersion", "$", issues, expected: 1);
        RequireNonEmptyString(root, "ProfileName", "$", issues);
        RequireEnum(root, "Status", "$", SnapshotStatuses, issues);
        RequireNonEmptyString(root, "Architecture", "$", issues);

        if (RequireArray(root, "ProviderResults", "$", issues, out JsonElement providers))
        {
            int index = 0;
            foreach (JsonElement provider in providers.EnumerateArray())
            {
                ValidateProvider(provider, $"$.ProviderResults[{index}]", issues);
                index++;
            }
        }

        if (RequireArray(root, "Artifacts", "$", issues, out JsonElement artifacts))
        {
            ValidateArtifacts(artifacts, "$.Artifacts", issues);
        }
    }

    private static void ValidateProvider(
        JsonElement provider,
        string path,
        List<SchemaValidationIssue> issues)
    {
        if (!RequireObject(provider, path, issues))
        {
            return;
        }

        RequireNonEmptyString(provider, "ProviderId", path, issues);
        RequireNonEmptyString(provider, "DisplayName", path, issues);
        RequireEnum(provider, "Status", path, ProviderStatuses, issues);
        RequireDateTime(provider, "StartedAtUtc", path, issues);
        RequireDateTime(provider, "FinishedAtUtc", path, issues);
        RequireNonNegativeInteger(provider, "ArtifactCount", path, issues);
        RequireStringArray(provider, "Warnings", path, issues);
        RequireStringArray(provider, "Errors", path, issues);
        RequireBoolean(provider, "RequiresAdministrator", path, issues);
        if (RequireArray(provider, "Artifacts", path, issues, out JsonElement artifacts))
        {
            ValidateArtifacts(artifacts, path + ".Artifacts", issues);
        }
    }

    private static void ValidateArtifacts(
        JsonElement artifacts,
        string path,
        List<SchemaValidationIssue> issues)
    {
        int index = 0;
        foreach (JsonElement artifact in artifacts.EnumerateArray())
        {
            string itemPath = $"{path}[{index}]";
            if (RequireObject(artifact, itemPath, issues))
            {
                RequireNonEmptyString(artifact, "ProviderId", itemPath, issues);
                RequireNonEmptyString(artifact, "ArtifactType", itemPath, issues);
                RequireNonEmptyString(artifact, "Identity", itemPath, issues);
                RequireNonEmptyString(artifact, "DisplayName", itemPath, issues);
                RequirePropertyKind(artifact, "Properties", JsonValueKind.Object, itemPath, issues);
                RequireStringArray(artifact, "Tags", itemPath, issues);
            }
            index++;
        }
    }

    private static void ValidateComparisonReport(
        JsonElement root,
        List<SchemaValidationIssue> issues)
    {
        if (!RequireObject(root, "$", issues))
        {
            return;
        }

        RequireExactString(root, "format", "SysDiff Comparison Report", "$", issues);
        RequireInteger(root, "formatVersion", "$", issues, expected: 1);
        RequireInteger(root, "schemaVersion", "$", issues, expected: 1);
        RequireVersionString(root, "sysDiffVersion", "$", issues);
        RequireDateTime(root, "generatedAtUtc", "$", issues);

        if (RequirePropertyKind(root, "before", JsonValueKind.Object, "$", issues, out JsonElement before))
        {
            ValidateSnapshotSummary(before, "$.before", issues);
        }
        if (RequirePropertyKind(root, "after", JsonValueKind.Object, "$", issues, out JsonElement after))
        {
            ValidateSnapshotSummary(after, "$.after", issues);
        }
        if (RequirePropertyKind(root, "comparison", JsonValueKind.Object, "$", issues, out JsonElement comparison))
        {
            ValidateComparison(comparison, "$.comparison", issues);
        }
    }

    private static void ValidateSnapshotSummary(
        JsonElement summary,
        string path,
        List<SchemaValidationIssue> issues)
    {
        RequireGuid(summary, "id", path, issues);
        RequireNonEmptyString(summary, "name", path, issues);
        RequireDateTime(summary, "createdAtUtc", path, issues);
        RequireNonEmptyString(summary, "profileName", path, issues);
        RequireEnum(summary, "status", path, SnapshotStatuses, issues);
    }

    private static void ValidateComparison(
        JsonElement comparison,
        string path,
        List<SchemaValidationIssue> issues)
    {
        RequireGuid(comparison, "id", path, issues);
        RequireGuid(comparison, "beforeSnapshotId", path, issues);
        RequireGuid(comparison, "afterSnapshotId", path, issues);
        RequireDateTime(comparison, "createdAtUtc", path, issues);
        RequireEnum(comparison, "noiseMode", path, NoiseModes, issues);
        RequireBoolean(comparison, "crossMachine", path, issues);
        RequireStringArray(comparison, "warnings", path, issues);
        RequireNonNegativeInteger(comparison, "hiddenAsNoise", path, issues);

        if (RequireArray(comparison, "changes", path, issues, out JsonElement changes))
        {
            int index = 0;
            foreach (JsonElement change in changes.EnumerateArray())
            {
                ValidateChange(change, $"{path}.changes[{index}]", issues);
                index++;
            }
        }
    }

    private static void ValidateChange(
        JsonElement change,
        string path,
        List<SchemaValidationIssue> issues)
    {
        if (!RequireObject(change, path, issues))
        {
            return;
        }
        RequireGuid(change, "id", path, issues);
        RequireEnum(change, "changeType", path, ChangeTypes, issues);
        RequireNonEmptyString(change, "providerId", path, issues);
        RequireNonEmptyString(change, "artifactType", path, issues);
        RequireNonEmptyString(change, "identity", path, issues);
        RequireNonEmptyString(change, "displayName", path, issues);
        RequirePropertyKind(change, "changedProperties", JsonValueKind.Array, path, issues);
        RequireEnum(change, "severity", path, Severities, issues);
        RequirePropertyKind(change, "explanation", JsonValueKind.String, path, issues);
        RequirePropertyKind(change, "whyThisMatters", JsonValueKind.String, path, issues);
        RequireStringArray(change, "tags", path, issues);
        RequireNumber(change, "confidence", path, issues, minimum: 0, maximum: 1);
        RequireBoolean(change, "isNoise", path, issues);
    }

    private static void ValidateBundleManifest(
        JsonElement root,
        List<SchemaValidationIssue> issues)
    {
        if (!RequireObject(root, "$", issues))
        {
            return;
        }

        RequireExactString(root, "format", "SysDiff Investigation Bundle", "$", issues);
        RequireInteger(root, "formatVersion", "$", issues, expected: 1);
        RequireInteger(root, "schemaVersion", "$", issues, expected: 1);
        RequireVersionString(root, "sysDiffVersion", "$", issues);
        RequireDateTime(root, "createdAtUtc", "$", issues);
        RequireGuid(root, "comparisonId", "$", issues);
        RequireGuid(root, "beforeSnapshotId", "$", issues);
        RequireGuid(root, "afterSnapshotId", "$", issues);
        RequireBoolean(root, "crossMachine", "$", issues);
        RequireStringArray(root, "warnings", "$", issues);

        if (RequirePropertyKind(root, "privacy", JsonValueKind.Object, "$", issues, out JsonElement privacy))
        {
            RequireBoolean(privacy, "userProfilePathsRedacted", "$.privacy", issues);
            RequireBoolean(privacy, "privateKeysIncluded", "$.privacy", issues);
            RequireBoolean(privacy, "rawLogsIncluded", "$.privacy", issues);
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

    private static bool RequireObject(
        JsonElement value,
        string path,
        List<SchemaValidationIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }
        AddIssue(issues, path, "type", "Ожидался JSON object.");
        return false;
    }

    private static bool RequirePropertyKind(
        JsonElement parent,
        string name,
        JsonValueKind kind,
        string path,
        List<SchemaValidationIssue> issues) =>
        RequirePropertyKind(parent, name, kind, path, issues, out _);

    private static bool RequirePropertyKind(
        JsonElement parent,
        string name,
        JsonValueKind kind,
        string path,
        List<SchemaValidationIssue> issues,
        out JsonElement value)
    {
        if (!parent.TryGetProperty(name, out value))
        {
            AddIssue(issues, path + "." + name, "required", "Обязательное поле отсутствует.");
            return false;
        }
        if (value.ValueKind != kind)
        {
            AddIssue(
                issues,
                path + "." + name,
                "type",
                $"Ожидался тип {kind}, получен {value.ValueKind}.");
            return false;
        }
        return true;
    }

    private static bool RequireArray(
        JsonElement parent,
        string name,
        string path,
        List<SchemaValidationIssue> issues,
        out JsonElement value) =>
        RequirePropertyKind(parent, name, JsonValueKind.Array, path, issues, out value);

    private static void RequireNonEmptyString(
        JsonElement parent,
        string name,
        string path,
        List<SchemaValidationIssue> issues)
    {
        if (RequirePropertyKind(parent, name, JsonValueKind.String, path, issues, out JsonElement value)
            && string.IsNullOrWhiteSpace(value.GetString()))
        {
            AddIssue(issues, path + "." + name, "min_length", "Строка не может быть пустой.");
        }
    }

    private static void RequireExactString(
        JsonElement parent,
        string name,
        string expected,
        string path,
        List<SchemaValidationIssue> issues)
    {
        if (RequirePropertyKind(parent, name, JsonValueKind.String, path, issues, out JsonElement value)
            && !string.Equals(value.GetString(), expected, StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                path + "." + name,
                "const",
                $"Ожидалось значение '{expected}'.");
        }
    }

    private static void RequireGuid(
        JsonElement parent,
        string name,
        string path,
        List<SchemaValidationIssue> issues)
    {
        if (RequirePropertyKind(parent, name, JsonValueKind.String, path, issues, out JsonElement value)
            && !Guid.TryParse(value.GetString(), out _))
        {
            AddIssue(issues, path + "." + name, "format", "Ожидался UUID.");
        }
    }

    private static void RequireDateTime(
        JsonElement parent,
        string name,
        string path,
        List<SchemaValidationIssue> issues)
    {
        if (RequirePropertyKind(parent, name, JsonValueKind.String, path, issues, out JsonElement value)
            && !DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _))
        {
            AddIssue(issues, path + "." + name, "format", "Ожидалась дата RFC 3339.");
        }
    }

    private static void RequireVersionString(
        JsonElement parent,
        string name,
        string path,
        List<SchemaValidationIssue> issues)
    {
        if (RequirePropertyKind(parent, name, JsonValueKind.String, path, issues, out JsonElement value)
            && !Regex.IsMatch(
                value.GetString() ?? string.Empty,
                "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:[-+][0-9A-Za-z.-]+)?$",
                RegexOptions.CultureInvariant))
        {
            AddIssue(issues, path + "." + name, "semver", "Ожидалась версия SemVer X.Y.Z.");
        }
    }

    private static void RequireInteger(
        JsonElement parent,
        string name,
        string path,
        List<SchemaValidationIssue> issues,
        int expected)
    {
        if (!RequirePropertyKind(parent, name, JsonValueKind.Number, path, issues, out JsonElement value))
        {
            return;
        }
        if (!value.TryGetInt32(out int parsed))
        {
            AddIssue(issues, path + "." + name, "integer", "Ожидалось целое число.");
        }
        else if (parsed != expected)
        {
            AddIssue(
                issues,
                path + "." + name,
                "const",
                $"Поддерживается только schema version {expected}.");
        }
    }

    private static void RequireNonNegativeInteger(
        JsonElement parent,
        string name,
        string path,
        List<SchemaValidationIssue> issues)
    {
        if (!RequirePropertyKind(parent, name, JsonValueKind.Number, path, issues, out JsonElement value))
        {
            return;
        }
        if (!value.TryGetInt32(out int parsed) || parsed < 0)
        {
            AddIssue(issues, path + "." + name, "minimum", "Ожидалось целое число >= 0.");
        }
    }

    private static void RequireNumber(
        JsonElement parent,
        string name,
        string path,
        List<SchemaValidationIssue> issues,
        double minimum,
        double maximum)
    {
        if (!RequirePropertyKind(parent, name, JsonValueKind.Number, path, issues, out JsonElement value))
        {
            return;
        }
        if (!value.TryGetDouble(out double parsed) || parsed < minimum || parsed > maximum)
        {
            AddIssue(
                issues,
                path + "." + name,
                "range",
                $"Ожидалось число в диапазоне {minimum}..{maximum}.");
        }
    }

    private static void RequireBoolean(
        JsonElement parent,
        string name,
        string path,
        List<SchemaValidationIssue> issues)
    {
        if (!parent.TryGetProperty(name, out JsonElement value))
        {
            AddIssue(issues, path + "." + name, "required", "Обязательное поле отсутствует.");
            return;
        }
        if (value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            AddIssue(issues, path + "." + name, "type", "Ожидался boolean.");
        }
    }

    private static void RequireEnum(
        JsonElement parent,
        string name,
        string path,
        HashSet<string> allowed,
        List<SchemaValidationIssue> issues)
    {
        if (RequirePropertyKind(parent, name, JsonValueKind.String, path, issues, out JsonElement value)
            && !allowed.Contains(value.GetString() ?? string.Empty))
        {
            AddIssue(
                issues,
                path + "." + name,
                "enum",
                $"Недопустимое значение. Разрешено: {string.Join(", ", allowed)}.");
        }
    }

    private static void RequireStringArray(
        JsonElement parent,
        string name,
        string path,
        List<SchemaValidationIssue> issues)
    {
        if (!RequireArray(parent, name, path, issues, out JsonElement array))
        {
            return;
        }
        int index = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                AddIssue(
                    issues,
                    $"{path}.{name}[{index}]",
                    "type",
                    "Ожидалась строка.");
            }
            index++;
        }
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

    private static void AddIssue(
        List<SchemaValidationIssue> issues,
        string path,
        string code,
        string message) =>
        issues.Add(new SchemaValidationIssue
        {
            Path = path,
            Code = code,
            Message = message
        });
}
