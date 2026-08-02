using System.Text.Json;
using System.Text.Json.Serialization;
using SysDiff.Storage;

namespace SysDiff.Cli;

internal sealed class V11CommandRouter
{
    private readonly V10CommandRouter _v10;
    private readonly PortableUpgradeService _legacy;

    public V11CommandRouter(
        V10CommandRouter v10,
        PortableUpgradeService legacy)
    {
        _v10 = v10;
        _legacy = legacy;
    }

    public async Task<int> RunAsync(
        string[] args,
        CommandApp fallback,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            return await _v10.RunAsync(args, fallback, cancellationToken);
        }

        if (args[0] is "--version" or "-v")
        {
            Console.WriteLine($"SysDiff {ProductInfo.Version}");
            return 0;
        }

        if (args[0].Equals("--tui-smoke", StringComparison.OrdinalIgnoreCase))
        {
            PrintSmokeFrame();
            return 0;
        }

        if (args[0].Equals("legacy", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("upgrade", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("bridge", StringComparison.OrdinalIgnoreCase))
        {
            return await RunLegacyAsync(args, cancellationToken);
        }

        int result = await _v10.RunAsync(args, fallback, cancellationToken);
        if (args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
        }
        return result;
    }

    private async Task<int> RunLegacyAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        string command = args.Length > 1 ? args[1].ToLowerInvariant() : "matrix";
        bool json = HasOption(args, "--json");

        switch (command)
        {
            case "matrix":
            case "list":
            case "catalog":
                if (json)
                {
                    WriteJson(new
                    {
                        productVersion = ProductInfo.Version,
                        targetSchemaVersion = 1,
                        sourceRange = "0.3.0-0.9.x",
                        formats = new object[]
                        {
                            new
                            {
                                kind = "comparison",
                                sourceShape = "pre-0.10 JSON report",
                                target = "SysDiff Comparison Report schema v1",
                                notes = "unknown producer version is recorded as 0.0.0-legacy"
                            },
                            new
                            {
                                kind = "bundle",
                                sourceShape = "pre-0.10 investigation ZIP",
                                target = "manifest/report schema v1",
                                notes = "nested snapshots are preserved byte-for-byte"
                            }
                        },
                        safety = new
                        {
                            planIsReadOnly = true,
                            explicitConfirmation = true,
                            automaticBackup = true,
                            atomicOutput = true,
                            postConversionValidation = true
                        }
                    });
                }
                else
                {
                    PrintMatrix();
                }
                return 0;

            case "status":
            case "plan":
            case "verify":
            {
                (PortableUpgradeKind kind, string input) = ParseTarget(args, command);
                PortableUpgradePlan plan = await _legacy.PlanAsync(
                    kind,
                    input,
                    cancellationToken);
                if (json)
                {
                    WriteJson(plan);
                }
                else
                {
                    PrintPlan(plan, detailed: command == "plan");
                }

                if (command == "verify")
                {
                    return plan.IsCurrent ? 0 : 4;
                }
                return plan.Status is PortableUpgradeStatus.Current
                    or PortableUpgradeStatus.UpgradeAvailable
                        ? 0
                        : 4;
            }

            case "convert":
            case "apply":
            {
                (PortableUpgradeKind kind, string input) = ParseTarget(args, command);
                if (!HasOption(args, "--yes"))
                {
                    throw new ArgumentException(
                        "Legacy conversion требует явного подтверждения: --yes");
                }
                string? output = GetOptionValue(args, "--output");
                bool overwrite = HasOption(args, "--overwrite");
                PortableUpgradeResult result = await _legacy.ConvertAsync(
                    kind,
                    input,
                    output,
                    overwrite,
                    cancellationToken);
                if (json)
                {
                    WriteJson(result);
                }
                else
                {
                    PrintResult(result);
                }
                return result.Success ? 0 : 4;
            }

            default:
                throw new ArgumentException(
                    "Команда legacy: matrix, status, plan, verify или convert.");
        }
    }

    private (PortableUpgradeKind Kind, string Input) ParseTarget(
        IReadOnlyList<string> args,
        string command)
    {
        if (args.Count < 4)
        {
            throw new ArgumentException(
                $"Использование: legacy {command} <comparison|bundle> <file> [options]");
        }
        return (_legacy.ParseKind(args[2]), args[3]);
    }

    private static void PrintMatrix()
    {
        Console.WriteLine("SysDiff Legacy Bridge");
        Console.WriteLine($"Product: {ProductInfo.Version}");
        Console.WriteLine("Target: public Schema Contract v1");
        Console.WriteLine("Supported source range: 0.3.0–0.9.x");
        Console.WriteLine();
        Console.WriteLine("comparison  pre-0.10 JSON report → comparison schema v1");
        Console.WriteLine("bundle      pre-0.10 investigation ZIP → manifest/report schema v1");
        Console.WriteLine();
        Console.WriteLine("Plan is read-only. Convert requires --yes and creates an automatic backup.");
        Console.WriteLine("Nested .sdshot files are preserved byte-for-byte.");
    }

    private static void PrintPlan(PortableUpgradePlan plan, bool detailed)
    {
        Console.WriteLine("Legacy Bridge plan");
        Console.WriteLine($"Kind: {plan.Kind}");
        Console.WriteLine($"Input: {plan.InputPath}");
        Console.WriteLine($"Status: {plan.Status}");
        Console.WriteLine($"Source shape: {plan.SourceShape}");
        Console.WriteLine($"Target schema: {plan.TargetSchemaVersion}");
        Console.WriteLine($"Backup: {(plan.RequiresBackup ? "required" : "not required")}");
        Console.WriteLine($"Suggested output: {plan.SuggestedOutputPath}");
        Console.WriteLine(plan.Message);

        if (detailed)
        {
            foreach (PortableUpgradeStep step in plan.Steps)
            {
                Console.WriteLine();
                Console.WriteLine($"[{step.Id}] → schema {step.TargetSchemaVersion}");
                Console.WriteLine($"  {step.Description}");
                Console.WriteLine($"  destructive: {step.Destructive}");
            }
        }

        foreach (string warning in plan.Warnings)
        {
            Console.WriteLine($"Warning: {warning}");
        }
    }

    private static void PrintResult(PortableUpgradeResult result)
    {
        Console.WriteLine(result.Success ? "Legacy conversion completed" : "Legacy conversion failed");
        Console.WriteLine(result.Message);
        Console.WriteLine($"Changed: {result.Changed}");
        Console.WriteLine($"Status: {result.StatusAfter}");
        if (!string.IsNullOrWhiteSpace(result.OutputPath))
        {
            Console.WriteLine($"Output: {result.OutputPath}");
        }
        if (!string.IsNullOrWhiteSpace(result.BackupPath))
        {
            Console.WriteLine($"Backup: {result.BackupPath}");
        }
        if (!string.IsNullOrWhiteSpace(result.SourceSha256))
        {
            Console.WriteLine($"Source SHA-256: {result.SourceSha256}");
        }
        if (!string.IsNullOrWhiteSpace(result.OutputSha256))
        {
            Console.WriteLine($"Output SHA-256: {result.OutputSha256}");
        }
        foreach (string step in result.AppliedStepIds)
        {
            Console.WriteLine($"Applied: {step}");
        }
    }

    private static bool HasOption(IReadOnlyList<string> args, string option) =>
        args.Any(value => value.Equals(option, StringComparison.OrdinalIgnoreCase));

    private static string? GetOptionValue(IReadOnlyList<string> args, string option)
    {
        for (int index = 0; index < args.Count; index++)
        {
            if (!args[index].Equals(option, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Параметр {option} требует значение.");
            }
            return args[index + 1];
        }
        return null;
    }

    private static void WriteJson<T>(T value) =>
        Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    private static void PrintSmokeFrame()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("SYSDIFF CYBER CONSOLE 0.11.0 // LEGACY BRIDGE");
        Console.WriteLine("[09] SYSTEM NODE > PLAN | BACKUP | CONVERT | VERIFY");
        Console.WriteLine("SOURCE: 0.3-0.9 | TARGET: SCHEMA V1 | OUTPUT: ATOMIC | SNAPSHOTS: PRESERVE");
        Console.WriteLine("================================================================================");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """

            LEGACY BRIDGE 0.11
              sysdiff legacy matrix [--json]
              sysdiff legacy status <comparison|bundle> <file> [--json]
              sysdiff legacy plan <comparison|bundle> <file> [--json]
              sysdiff legacy verify <comparison|bundle> <file> [--json]
              sysdiff legacy convert <comparison|bundle> <file> [--output <file>] --yes [--overwrite] [--json]

            Plan является read-only. Convert всегда создаёт backup исходника и пишет результат атомарно.
            Comparison reports 0.3–0.9 получают Schema Contract v1 без изменения payload.
            Bundle manifest/report преобразуются, checksums пересчитываются, вложенные .sdshot не переписываются.
            """);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
