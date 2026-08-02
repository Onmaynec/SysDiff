using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SysDiff.Domain;

namespace SysDiff.Storage;

public sealed class PortableUpgradeService
{
    private const long MaximumJsonBytes = 256L * 1024L * 1024L;
    private const long MaximumBundleBytes = 512L * 1024L * 1024L;
    private const long MaximumUncompressedBytes = 1024L * 1024L * 1024L;
    private const int MaximumBundleEntries = 64;
    private const string LegacyProducerVersion = "0.0.0-legacy";

    private readonly SchemaContractService _schemas;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public PortableUpgradeService(SchemaContractService schemas)
    {
        _schemas = schemas;
    }

    public PortableUpgradeKind ParseKind(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().ToLowerInvariant() switch
        {
            "comparison" or "report" or "comparison-report" or "json" =>
                PortableUpgradeKind.ComparisonReport,
            "bundle" or "investigation" or "investigation-bundle" or "zip" =>
                PortableUpgradeKind.InvestigationBundle,
            _ => throw new ArgumentException(
                "Legacy kind: comparison или bundle.",
                nameof(value))
        };
    }

    public Task<PortableUpgradePlan> PlanAsync(
        PortableUpgradeKind kind,
        string inputPath,
        CancellationToken cancellationToken) =>
        kind switch
        {
            PortableUpgradeKind.ComparisonReport =>
                PlanComparisonAsync(inputPath, cancellationToken),
            PortableUpgradeKind.InvestigationBundle =>
                PlanBundleAsync(inputPath, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    public async Task<PortableUpgradeResult> ConvertAsync(
        PortableUpgradeKind kind,
        string inputPath,
        string? outputPath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        PortableUpgradePlan plan = await PlanAsync(kind, inputPath, cancellationToken);
        if (plan.IsCurrent)
        {
            return new PortableUpgradeResult
            {
                Kind = kind,
                InputPath = plan.InputPath,
                Success = true,
                Changed = false,
                StatusAfter = PortableUpgradeStatus.Current,
                Message = "Файл уже соответствует Schema Contract v1. Изменения не требуются."
            };
        }

        if (!plan.CanConvert)
        {
            return new PortableUpgradeResult
            {
                Kind = kind,
                InputPath = plan.InputPath,
                Success = false,
                Changed = false,
                StatusAfter = plan.Status,
                Message = plan.Message
            };
        }

        string fullInput = plan.InputPath;
        string fullOutput = Path.GetFullPath(
            string.IsNullOrWhiteSpace(outputPath)
                ? plan.SuggestedOutputPath
                : outputPath);
        bool replacingSource = PathsEqual(fullInput, fullOutput);

        if (File.Exists(fullOutput) && !overwrite)
        {
            return new PortableUpgradeResult
            {
                Kind = kind,
                InputPath = fullInput,
                OutputPath = fullOutput,
                Success = false,
                Changed = false,
                StatusAfter = PortableUpgradeStatus.Invalid,
                Message = "Выходной файл уже существует. Укажите другой путь или --overwrite."
            };
        }

        string sourceHash = await ComputeSha256Async(fullInput, cancellationToken);
        string backupPath = await CreateBackupAsync(fullInput, cancellationToken);

        try
        {
            switch (kind)
            {
                case PortableUpgradeKind.ComparisonReport:
                    await ConvertComparisonAsync(
                        fullInput,
                        fullOutput,
                        overwrite,
                        cancellationToken);
                    break;
                case PortableUpgradeKind.InvestigationBundle:
                    await ConvertBundleAsync(
                        fullInput,
                        fullOutput,
                        overwrite,
                        cancellationToken);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }

            PortableUpgradePlan after = await PlanAsync(kind, fullOutput, cancellationToken);
            if (!after.IsCurrent)
            {
                throw new InvalidDataException(
                    $"Проверка преобразованного файла завершилась статусом {after.Status}: {after.Message}");
            }

            string outputHash = await ComputeSha256Async(fullOutput, cancellationToken);
            return new PortableUpgradeResult
            {
                Kind = kind,
                InputPath = fullInput,
                OutputPath = fullOutput,
                BackupPath = backupPath,
                Success = true,
                Changed = true,
                StatusAfter = PortableUpgradeStatus.Current,
                SourceSha256 = sourceHash,
                OutputSha256 = outputHash,
                AppliedStepIds = plan.Steps.Select(value => value.Id).ToList(),
                Message = "Legacy portable data преобразованы в Schema Contract v1 и повторно проверены."
            };
        }
        catch (Exception exception) when (
            exception is IOException
            or InvalidDataException
            or JsonException
            or UnauthorizedAccessException)
        {
            try
            {
                if (replacingSource)
                {
                    await RestoreBackupAsync(backupPath, fullInput, cancellationToken);
                }
                else if (File.Exists(fullOutput))
                {
                    File.Delete(fullOutput);
                }
            }
            catch
            {
                // Backup остаётся доступным для ручного recovery.
            }

            return new PortableUpgradeResult
            {
                Kind = kind,
                InputPath = fullInput,
                OutputPath = fullOutput,
                BackupPath = backupPath,
                Success = false,
                Changed = false,
                StatusAfter = PortableUpgradeStatus.Invalid,
                SourceSha256 = sourceHash,
                AppliedStepIds = plan.Steps.Select(value => value.Id).ToList(),
                Message = $"Преобразование отменено: {exception.Message}"
            };
        }
    }

    private async Task<PortableUpgradePlan> PlanComparisonAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(inputPath);
        string suggested = SuggestOutputPath(fullPath, "schema-v1");
        try
        {
            byte[] bytes = await ReadFileAsync(fullPath, MaximumJsonBytes, cancellationToken);
            JsonObject root = ParseObject(bytes, "comparison report");
            int? schemaVersion = ReadInt(root, "schemaVersion");
            if (schemaVersion > SysDiffProduct.PublicSchemaVersion)
            {
                return Plan(
                    PortableUpgradeKind.ComparisonReport,
                    fullPath,
                    suggested,
                    PortableUpgradeStatus.RequiresNewerSysDiff,
                    "comparison-report-future",
                    "Документ использует более новую public schema.");
            }

            if (IsCurrentComparisonShape(root))
            {
                SchemaValidationResult validation = _schemas.ValidateJson(
                    SchemaContractKind.ComparisonReport,
                    root.ToJsonString(),
                    fullPath);
                return ValidationPlan(
                    PortableUpgradeKind.ComparisonReport,
                    fullPath,
                    suggested,
                    validation,
                    "comparison-report-v1");
            }

            if (!IsLegacyComparisonShape(root))
            {
                return Plan(
                    PortableUpgradeKind.ComparisonReport,
                    fullPath,
                    suggested,
                    schemaVersion is < 1
                        ? PortableUpgradeStatus.UnsupportedLegacy
                        : PortableUpgradeStatus.Invalid,
                    "comparison-report-unknown",
                    "Форма comparison report не распознана как поддерживаемая legacy 0.3–0.9.");
            }

            JsonObject upgraded = UpgradeLegacyComparison(root, DateTimeOffset.UnixEpoch);
            SchemaValidationResult targetValidation = _schemas.ValidateJson(
                SchemaContractKind.ComparisonReport,
                upgraded.ToJsonString(),
                fullPath);
            if (!targetValidation.IsValid)
            {
                return Plan(
                    PortableUpgradeKind.ComparisonReport,
                    fullPath,
                    suggested,
                    PortableUpgradeStatus.Invalid,
                    "comparison-report-pre-0.10",
                    "Legacy report не может быть преобразован без потери обязательных данных.",
                    warnings: targetValidation.Issues.Select(FormatIssue));
            }

            return Plan(
                PortableUpgradeKind.ComparisonReport,
                fullPath,
                suggested,
                PortableUpgradeStatus.UpgradeAvailable,
                "comparison-report-pre-0.10",
                "Доступно безопасное преобразование legacy comparison report в Schema Contract v1.",
                requiresBackup: true,
                steps:
                [
                    new PortableUpgradeStep
                    {
                        Id = "0.11.0-comparison-contract-v1",
                        Description =
                            "Добавить format/formatVersion/sysDiffVersion и migration provenance без изменения comparison payload."
                    }
                ],
                warnings:
                [
                    "Legacy comparison report не содержит достоверную producer version; используется sentinel 0.0.0-legacy."
                ]);
        }
        catch (FileNotFoundException)
        {
            return Plan(
                PortableUpgradeKind.ComparisonReport,
                fullPath,
                suggested,
                PortableUpgradeStatus.Invalid,
                "missing",
                "Входной файл не найден.");
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or JsonException)
        {
            return Plan(
                PortableUpgradeKind.ComparisonReport,
                fullPath,
                suggested,
                PortableUpgradeStatus.Invalid,
                "invalid",
                exception.Message);
        }
    }

    private async Task<PortableUpgradePlan> PlanBundleAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(inputPath);
        string suggested = SuggestOutputPath(fullPath, "schema-v1");
        try
        {
            BundlePackage package = await ReadBundleAsync(fullPath, cancellationToken);
            int? manifestSchema = ReadInt(package.Manifest, "schemaVersion");
            int? reportSchema = ReadInt(package.Report, "schemaVersion");
            if (manifestSchema > SysDiffProduct.PublicSchemaVersion
                || reportSchema > SysDiffProduct.PublicSchemaVersion)
            {
                return Plan(
                    PortableUpgradeKind.InvestigationBundle,
                    fullPath,
                    suggested,
                    PortableUpgradeStatus.RequiresNewerSysDiff,
                    "investigation-bundle-future",
                    "Bundle использует более новую public schema.");
            }

            bool currentManifest = IsCurrentBundleManifestShape(package.Manifest);
            bool currentReport = IsCurrentComparisonShape(package.Report);
            if (currentManifest && currentReport)
            {
                SchemaValidationResult manifestValidation = _schemas.ValidateJson(
                    SchemaContractKind.InvestigationBundleManifest,
                    package.Manifest.ToJsonString(),
                    fullPath + "!manifest.json");
                SchemaValidationResult reportValidation = _schemas.ValidateJson(
                    SchemaContractKind.ComparisonReport,
                    package.Report.ToJsonString(),
                    fullPath + "!report.json");
                if (manifestValidation.IsValid && reportValidation.IsValid)
                {
                    return Plan(
                        PortableUpgradeKind.InvestigationBundle,
                        fullPath,
                        suggested,
                        PortableUpgradeStatus.Current,
                        "investigation-bundle-v1",
                        "Bundle соответствует Schema Contract v1, а все payload checksums корректны.");
                }

                return Plan(
                    PortableUpgradeKind.InvestigationBundle,
                    fullPath,
                    suggested,
                    manifestValidation.Status == SchemaValidationStatus.RequiresNewerSysDiff
                        || reportValidation.Status == SchemaValidationStatus.RequiresNewerSysDiff
                            ? PortableUpgradeStatus.RequiresNewerSysDiff
                            : PortableUpgradeStatus.Invalid,
                    "investigation-bundle-v1-invalid",
                    "Bundle содержит contract metadata v1, но payload validation не пройдена.",
                    warnings: manifestValidation.Issues.Select(FormatIssue)
                        .Concat(reportValidation.Issues.Select(FormatIssue)));
            }

            bool legacyManifest = IsLegacyBundleManifestShape(package.Manifest);
            bool legacyReport = IsLegacyComparisonShape(package.Report);
            if ((!currentManifest && !legacyManifest)
                || (!currentReport && !legacyReport))
            {
                return Plan(
                    PortableUpgradeKind.InvestigationBundle,
                    fullPath,
                    suggested,
                    PortableUpgradeStatus.UnsupportedLegacy,
                    "investigation-bundle-unknown",
                    "Bundle не соответствует поддерживаемой форме 0.3–0.9 или Schema Contract v1.");
            }

            DateTimeOffset marker = DateTimeOffset.UnixEpoch;
            JsonObject targetManifest = legacyManifest
                ? UpgradeLegacyBundleManifest(package.Manifest, marker)
                : CloneObject(package.Manifest);
            JsonObject targetReport = legacyReport
                ? UpgradeLegacyComparison(package.Report, marker)
                : CloneObject(package.Report);
            SchemaValidationResult upgradedManifest = _schemas.ValidateJson(
                SchemaContractKind.InvestigationBundleManifest,
                targetManifest.ToJsonString(),
                fullPath + "!manifest.json");
            SchemaValidationResult upgradedReport = _schemas.ValidateJson(
                SchemaContractKind.ComparisonReport,
                targetReport.ToJsonString(),
                fullPath + "!report.json");
            if (!upgradedManifest.IsValid || !upgradedReport.IsValid)
            {
                return Plan(
                    PortableUpgradeKind.InvestigationBundle,
                    fullPath,
                    suggested,
                    PortableUpgradeStatus.Invalid,
                    "investigation-bundle-pre-0.10",
                    "Legacy bundle не может быть преобразован без потери обязательных данных.",
                    warnings: upgradedManifest.Issues.Select(FormatIssue)
                        .Concat(upgradedReport.Issues.Select(FormatIssue)));
            }

            var steps = new List<PortableUpgradeStep>();
            if (legacyManifest)
            {
                steps.Add(new PortableUpgradeStep
                {
                    Id = "0.11.0-bundle-manifest-contract-v1",
                    Description = "Добавить schemaVersion и migration provenance в manifest.json."
                });
            }
            if (legacyReport)
            {
                steps.Add(new PortableUpgradeStep
                {
                    Id = "0.11.0-bundle-report-contract-v1",
                    Description = "Преобразовать report.json в comparison Schema Contract v1."
                });
            }
            steps.Add(new PortableUpgradeStep
            {
                Id = "0.11.0-bundle-checksums",
                Description = "Пересчитать SHA-256 для каждого payload entry после преобразования."
            });

            return Plan(
                PortableUpgradeKind.InvestigationBundle,
                fullPath,
                suggested,
                PortableUpgradeStatus.UpgradeAvailable,
                legacyManifest && legacyReport
                    ? "investigation-bundle-pre-0.10"
                    : "investigation-bundle-mixed-v1",
                "Доступно безопасное преобразование bundle в Schema Contract v1.",
                requiresBackup: true,
                steps: steps,
                warnings:
                [
                    "Вложенные .sdshot копируются byte-for-byte и не переписываются.",
                    "Исходная producer version legacy comparison report неизвестна; используется 0.0.0-legacy."
                ]);
        }
        catch (FileNotFoundException)
        {
            return Plan(
                PortableUpgradeKind.InvestigationBundle,
                fullPath,
                suggested,
                PortableUpgradeStatus.Invalid,
                "missing",
                "Входной bundle не найден.");
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or JsonException)
        {
            return Plan(
                PortableUpgradeKind.InvestigationBundle,
                fullPath,
                suggested,
                PortableUpgradeStatus.Invalid,
                "invalid",
                exception.Message);
        }
    }

    private async Task ConvertComparisonAsync(
        string inputPath,
        string outputPath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        byte[] source = await ReadFileAsync(inputPath, MaximumJsonBytes, cancellationToken);
        JsonObject root = ParseObject(source, "comparison report");
        JsonObject target = UpgradeLegacyComparison(root, DateTimeOffset.UtcNow);
        SchemaValidationResult validation = _schemas.ValidateJson(
            SchemaContractKind.ComparisonReport,
            target.ToJsonString(),
            outputPath);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(
                "Преобразованный comparison report не прошёл Schema Contract validation: " +
                string.Join("; ", validation.Issues.Select(FormatIssue)));
        }

        byte[] content = JsonSerializer.SerializeToUtf8Bytes(target, JsonOptions);
        await WriteAtomicAsync(outputPath, content, overwrite, cancellationToken);
    }

    private async Task ConvertBundleAsync(
        string inputPath,
        string outputPath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        BundlePackage package = await ReadBundleAsync(inputPath, cancellationToken);
        var payload = package.Entries
            .Where(value => !value.Key.Equals("checksums.sha256", StringComparison.Ordinal))
            .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);

        if (IsLegacyBundleManifestShape(package.Manifest))
        {
            JsonObject manifest = UpgradeLegacyBundleManifest(package.Manifest, DateTimeOffset.UtcNow);
            payload["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        }
        if (IsLegacyComparisonShape(package.Report))
        {
            JsonObject report = UpgradeLegacyComparison(package.Report, DateTimeOffset.UtcNow);
            payload["report.json"] = JsonSerializer.SerializeToUtf8Bytes(report, JsonOptions);
        }

        payload["checksums.sha256"] = CreateChecksumFile(payload);
        await WriteBundleAtomicAsync(outputPath, payload, overwrite, cancellationToken);
    }

    private static JsonObject UpgradeLegacyComparison(
        JsonObject source,
        DateTimeOffset migratedAtUtc)
    {
        var target = new JsonObject
        {
            ["format"] = "SysDiff Comparison Report",
            ["formatVersion"] = 1,
            ["schemaVersion"] = SysDiffProduct.PublicSchemaVersion,
            ["sysDiffVersion"] = LegacyProducerVersion
        };

        foreach ((string key, JsonNode? value) in source)
        {
            if (key is "format" or "formatVersion" or "schemaVersion" or "sysDiffVersion"
                or "legacyMigration")
            {
                continue;
            }
            target[key] = value?.DeepClone();
        }

        target["legacyMigration"] = new JsonObject
        {
            ["sourceShape"] = "comparison-report-pre-0.10",
            ["producerVersionKnown"] = false,
            ["migratedByVersion"] = SysDiffProduct.Version,
            ["migratedAtUtc"] = migratedAtUtc.ToString("O", CultureInfo.InvariantCulture)
        };
        return target;
    }

    private static JsonObject UpgradeLegacyBundleManifest(
        JsonObject source,
        DateTimeOffset migratedAtUtc)
    {
        var target = new JsonObject();
        foreach ((string key, JsonNode? value) in source)
        {
            if (key is "schemaVersion" or "legacyMigration")
            {
                continue;
            }
            target[key] = value?.DeepClone();
        }
        target["schemaVersion"] = SysDiffProduct.PublicSchemaVersion;
        target["legacyMigration"] = new JsonObject
        {
            ["sourceShape"] = "investigation-bundle-manifest-pre-0.10",
            ["producerVersionValuePreserved"] = true,
            ["migratedByVersion"] = SysDiffProduct.Version,
            ["migratedAtUtc"] = migratedAtUtc.ToString("O", CultureInfo.InvariantCulture)
        };
        return target;
    }

    private static bool IsCurrentComparisonShape(JsonObject root) =>
        ReadString(root, "format") == "SysDiff Comparison Report"
        && ReadInt(root, "formatVersion") == 1
        && ReadInt(root, "schemaVersion") == SysDiffProduct.PublicSchemaVersion
        && !string.IsNullOrWhiteSpace(ReadString(root, "sysDiffVersion"));

    private static bool IsLegacyComparisonShape(JsonObject root) =>
        ReadInt(root, "schemaVersion") == 1
        && !root.ContainsKey("format")
        && !root.ContainsKey("formatVersion")
        && !root.ContainsKey("sysDiffVersion")
        && root["generatedAtUtc"] is JsonValue
        && root["before"] is JsonObject
        && root["after"] is JsonObject
        && root["comparison"] is JsonObject;

    private static bool IsCurrentBundleManifestShape(JsonObject root) =>
        ReadString(root, "format") == "SysDiff Investigation Bundle"
        && ReadInt(root, "formatVersion") == 1
        && ReadInt(root, "schemaVersion") == SysDiffProduct.PublicSchemaVersion
        && !string.IsNullOrWhiteSpace(ReadString(root, "sysDiffVersion"));

    private static bool IsLegacyBundleManifestShape(JsonObject root) =>
        ReadString(root, "format") == "SysDiff Investigation Bundle"
        && ReadInt(root, "formatVersion") == 1
        && !root.ContainsKey("schemaVersion")
        && !string.IsNullOrWhiteSpace(ReadString(root, "sysDiffVersion"))
        && root["createdAtUtc"] is JsonValue
        && root["comparisonId"] is JsonValue
        && root["beforeSnapshotId"] is JsonValue
        && root["afterSnapshotId"] is JsonValue
        && root["privacy"] is JsonObject;

    private static PortableUpgradePlan ValidationPlan(
        PortableUpgradeKind kind,
        string inputPath,
        string suggested,
        SchemaValidationResult validation,
        string sourceShape)
    {
        PortableUpgradeStatus status = validation.Status switch
        {
            SchemaValidationStatus.Valid => PortableUpgradeStatus.Current,
            SchemaValidationStatus.RequiresNewerSysDiff =>
                PortableUpgradeStatus.RequiresNewerSysDiff,
            _ => PortableUpgradeStatus.Invalid
        };
        return Plan(
            kind,
            inputPath,
            suggested,
            status,
            sourceShape,
            validation.IsValid
                ? "Документ соответствует Schema Contract v1."
                : "Contract validation не пройдена.",
            warnings: validation.Issues.Select(FormatIssue));
    }

    private static PortableUpgradePlan Plan(
        PortableUpgradeKind kind,
        string inputPath,
        string suggestedOutputPath,
        PortableUpgradeStatus status,
        string sourceShape,
        string message,
        bool requiresBackup = false,
        IEnumerable<PortableUpgradeStep>? steps = null,
        IEnumerable<string>? warnings = null) =>
        new()
        {
            Kind = kind,
            InputPath = inputPath,
            Status = status,
            SourceShape = sourceShape,
            SuggestedOutputPath = suggestedOutputPath,
            RequiresBackup = requiresBackup,
            Steps = steps?.ToList() ?? [],
            Warnings = warnings?.ToList() ?? [],
            Message = message
        };

    private static string SuggestOutputPath(string inputPath, string suffix)
    {
        string directory = Path.GetDirectoryName(inputPath) ?? ".";
        string extension = Path.GetExtension(inputPath);
        string name = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(directory, $"{name}.{suffix}{extension}");
    }

    private static JsonObject ParseObject(byte[] bytes, string label)
    {
        JsonNode? node = JsonNode.Parse(Encoding.UTF8.GetString(bytes));
        return node as JsonObject
            ?? throw new InvalidDataException($"{label} должен содержать JSON object.");
    }

    private static JsonObject CloneObject(JsonObject source) =>
        source.DeepClone() as JsonObject
        ?? throw new InvalidDataException("Не удалось клонировать JSON object.");

    private static int? ReadInt(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonValue value
            && value.TryGetValue<int>(out int result))
        {
            return result;
        }
        return null;
    }

    private static string? ReadString(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonValue value
            && value.TryGetValue<string>(out string? result))
        {
            return result;
        }
        return null;
    }

    private static string FormatIssue(SchemaValidationIssue issue) =>
        $"{issue.Path} [{issue.Code}] {issue.Message}";

    private static async Task<byte[]> ReadFileAsync(
        string fullPath,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Файл не найден.", fullPath);
        }
        if (info.Length > maximumBytes)
        {
            throw new InvalidDataException(
                $"Файл превышает допустимый размер {maximumBytes} байт.");
        }
        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    private static async Task<BundlePackage> ReadBundleAsync(
        string fullPath,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Bundle не найден.", fullPath);
        }
        if (info.Length > MaximumBundleBytes)
        {
            throw new InvalidDataException("Bundle превышает допустимый размер 512 МБ.");
        }

        await using FileStream stream = new(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            useAsync: true);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        if (archive.Entries.Count is 0 or > MaximumBundleEntries)
        {
            throw new InvalidDataException("Bundle содержит недопустимое количество entries.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long total = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            ValidateEntry(entry);
            if (!names.Add(entry.FullName))
            {
                throw new InvalidDataException($"Bundle содержит duplicate entry: {entry.FullName}.");
            }
            total += entry.Length;
            if (total > MaximumUncompressedBytes)
            {
                throw new InvalidDataException("Распакованный bundle превышает допустимый размер 1 ГБ.");
            }
            entries[entry.FullName] = await ReadEntryAsync(
                entry,
                MaximumUncompressedBytes,
                cancellationToken);
        }

        foreach (string required in new[]
                 {
                     "manifest.json",
                     "report.json",
                     "before.sdshot",
                     "after.sdshot",
                     "checksums.sha256"
                 })
        {
            if (!entries.ContainsKey(required))
            {
                throw new InvalidDataException($"Bundle не содержит обязательный entry {required}.");
            }
        }

        VerifyBundleChecksums(entries);
        JsonObject manifest = ParseObject(entries["manifest.json"], "manifest.json");
        JsonObject report = ParseObject(entries["report.json"], "report.json");
        return new BundlePackage(entries, manifest, report);
    }

    private static void ValidateEntry(ZipArchiveEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.FullName)
            || !string.Equals(entry.FullName, entry.Name, StringComparison.Ordinal)
            || entry.FullName.Contains("..", StringComparison.Ordinal)
            || entry.FullName.Contains(':')
            || Path.IsPathRooted(entry.FullName))
        {
            throw new InvalidDataException("Bundle содержит небезопасный ZIP path.");
        }
    }

    private static async Task<byte[]> ReadEntryAsync(
        ZipArchiveEntry entry,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (entry.Length > maximumBytes)
        {
            throw new InvalidDataException($"Entry {entry.FullName} превышает лимит.");
        }
        await using Stream input = entry.Open();
        using var output = new MemoryStream();
        byte[] buffer = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maximumBytes)
            {
                throw new InvalidDataException($"Entry {entry.FullName} превышает лимит.");
            }
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return output.ToArray();
    }

    private static void VerifyBundleChecksums(IReadOnlyDictionary<string, byte[]> entries)
    {
        string text = Encoding.ASCII.GetString(entries["checksums.sha256"]);
        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string rawLine in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length < 67
                || line[64] != ' '
                || line[65] != ' '
                || !line[..64].All(Uri.IsHexDigit))
            {
                throw new InvalidDataException("checksums.sha256 содержит некорректную строку.");
            }
            string name = line[66..];
            if (string.IsNullOrWhiteSpace(name)
                || name.Contains("..", StringComparison.Ordinal)
                || Path.IsPathRooted(name)
                || !declared.TryAdd(name, line[..64].ToLowerInvariant()))
            {
                throw new InvalidDataException("checksums.sha256 содержит небезопасный или duplicate path.");
            }
        }

        foreach ((string name, byte[] content) in entries)
        {
            if (name.Equals("checksums.sha256", StringComparison.Ordinal))
            {
                continue;
            }
            if (!declared.TryGetValue(name, out string? expected))
            {
                throw new InvalidDataException($"Для entry {name} отсутствует SHA-256.");
            }
            string actual = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"SHA-256 entry {name} не совпадает.");
            }
        }

        foreach (string name in declared.Keys)
        {
            if (!entries.ContainsKey(name))
            {
                throw new InvalidDataException($"Checksum ссылается на отсутствующий entry {name}.");
            }
        }
    }

    private static byte[] CreateChecksumFile(IReadOnlyDictionary<string, byte[]> entries)
    {
        var builder = new StringBuilder();
        foreach ((string name, byte[] content) in entries
                     .Where(value => !value.Key.Equals("checksums.sha256", StringComparison.Ordinal))
                     .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
        {
            string hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            builder.Append(hash).Append("  ").Append(name).Append('\n');
        }
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static async Task WriteBundleAtomicAsync(
        string outputPath,
        IReadOnlyDictionary<string, byte[]> entries,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
        string temporary = fullPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream file = new(
                temporary,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                64 * 1024,
                useAsync: true))
            {
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach ((string name, byte[] content) in entries
                                 .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                        await using Stream output = entry.Open();
                        await output.WriteAsync(content, cancellationToken);
                    }
                }
                await file.FlushAsync(cancellationToken);
            }
            File.Move(temporary, fullPath, overwrite);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static async Task WriteAtomicAsync(
        string outputPath,
        byte[] content,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
        string temporary = fullPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream file = new(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                useAsync: true))
            {
                await file.WriteAsync(content, cancellationToken);
                await file.FlushAsync(cancellationToken);
            }
            File.Move(temporary, fullPath, overwrite);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static async Task<string> CreateBackupAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(inputPath) ?? ".";
        string extension = Path.GetExtension(inputPath);
        string name = Path.GetFileNameWithoutExtension(inputPath);
        string stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        string backup = Path.Combine(
            directory,
            $"{name}.legacy-backup-{stamp}-{Guid.NewGuid():N}{extension}");
        await CopyFileAsync(inputPath, backup, overwrite: false, cancellationToken);
        return backup;
    }

    private static Task RestoreBackupAsync(
        string backupPath,
        string destination,
        CancellationToken cancellationToken) =>
        CopyFileAsync(backupPath, destination, overwrite: true, cancellationToken);

    private static async Task CopyFileAsync(
        string source,
        string destination,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        string temporary = destination + $".{Guid.NewGuid():N}.copy";
        try
        {
            await using FileStream input = new(
                source,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                useAsync: true);
            await using (FileStream output = new(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                useAsync: true))
            {
                await input.CopyToAsync(output, 64 * 1024, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            File.Move(temporary, destination, overwrite);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            useAsync: true);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private sealed record BundlePackage(
        Dictionary<string, byte[]> Entries,
        JsonObject Manifest,
        JsonObject Report);
}
