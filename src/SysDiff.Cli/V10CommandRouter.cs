using System.Text.Json;
using System.Text.Json.Serialization;
using SysDiff.Storage;

namespace SysDiff.Cli;

internal sealed class V10CommandRouter
{
    private readonly V9CommandRouter _v9;
    private readonly SchemaContractService _schemas;

    public V10CommandRouter(
        V9CommandRouter v9,
        SchemaContractService schemas)
    {
        _v9 = v9;
        _schemas = schemas;
    }

    public async Task<int> RunAsync(
        string[] args,
        CommandApp fallback,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            return await _v9.RunAsync(args, fallback, cancellationToken);
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

        if (args[0].Equals("schema", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("schemas", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("contract", StringComparison.OrdinalIgnoreCase))
        {
            return await RunSchemaAsync(args, cancellationToken);
        }

        int result = await _v9.RunAsync(args, fallback, cancellationToken);
        if (args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
        }
        return result;
    }

    private async Task<int> RunSchemaAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        string command = args.Length > 1 ? args[1].ToLowerInvariant() : "list";
        bool json = HasOption(args, "--json");

        switch (command)
        {
            case "list":
            case "matrix":
            case "catalog":
            {
                IReadOnlyList<SchemaContractDescriptor> contracts = _schemas.ListContracts();
                if (json)
                {
                    WriteJson(new
                    {
                        productVersion = ProductInfo.Version,
                        contractVersion = 1,
                        jsonSchemaDraft = "2020-12",
                        compatibilityPolicy =
                            "unknown additive properties are allowed; breaking changes require a new schema major",
                        contracts
                    });
                }
                else
                {
                    PrintCatalog(contracts);
                }
                return 0;
            }
            case "show":
            {
                if (args.Length < 3)
                {
                    throw new ArgumentException(
                        "Использование: schema show <snapshot|comparison|bundle>");
                }

                SchemaContractKind kind = _schemas.ParseKind(args[2]);
                Console.WriteLine(_schemas.GetSchemaJson(kind));
                return 0;
            }
            case "validate":
            case "verify":
            {
                if (args.Length < 4)
                {
                    throw new ArgumentException(
                        "Использование: schema validate <snapshot|comparison|bundle> <file.json> [--json]");
                }

                SchemaContractKind kind = _schemas.ParseKind(args[2]);
                SchemaValidationResult result = await _schemas.ValidateFileAsync(
                    kind,
                    args[3],
                    cancellationToken);
                if (json)
                {
                    WriteJson(result);
                }
                else
                {
                    PrintValidation(result);
                }
                return result.IsValid ? 0 : 4;
            }
            default:
                throw new ArgumentException(
                    "Команда schema: list, matrix, show, validate или verify.");
        }
    }

    private static void PrintCatalog(IReadOnlyList<SchemaContractDescriptor> contracts)
    {
        Console.WriteLine("SysDiff Schema Contract Center");
        Console.WriteLine($"Product: {ProductInfo.Version}");
        Console.WriteLine("JSON Schema: Draft 2020-12");
        Console.WriteLine("Contract major: 1 (stable)");
        Console.WriteLine("Policy: unknown additive properties are allowed.");
        Console.WriteLine("Breaking changes require schema major 2 and a migration guide.");
        Console.WriteLine();
        foreach (SchemaContractDescriptor contract in contracts)
        {
            Console.WriteLine(
                $"{contract.Key,-12} v{contract.SchemaVersion}  " +
                $"{contract.Stability,-6}  {contract.FileName}");
            Console.WriteLine($"  {contract.SchemaId}");
        }
    }

    private static void PrintValidation(SchemaValidationResult result)
    {
        Console.WriteLine("Schema Contract validation");
        Console.WriteLine($"Contract: {result.Contract.DisplayName} v{result.Contract.SchemaVersion}");
        Console.WriteLine($"Input: {result.InputPath}");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Document schema: {result.DocumentSchemaVersion?.ToString() ?? "unknown"}");
        Console.WriteLine($"Valid: {result.IsValid}");
        foreach (SchemaValidationIssue issue in result.Issues)
        {
            Console.WriteLine($"{issue.Path} [{issue.Code}] {issue.Message}");
        }
        foreach (string warning in result.Warnings)
        {
            Console.WriteLine($"Policy: {warning}");
        }
    }

    private static bool HasOption(IReadOnlyList<string> args, string option) =>
        args.Any(value => value.Equals(option, StringComparison.OrdinalIgnoreCase));

    private static void WriteJson<T>(T value) =>
        Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    private static void PrintSmokeFrame()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("SYSDIFF CYBER CONSOLE 0.10.0 // SCHEMA CONTRACT CENTER");
        Console.WriteLine("[09] SYSTEM NODE > CONTRACT CATALOG | GOLDEN FIXTURES | VALIDATION");
        Console.WriteLine("SCHEMA: V1 STABLE | DRAFT: 2020-12 | ADDITIVE: ALLOW | BREAKING: MAJOR");
        Console.WriteLine("================================================================================");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """

            SCHEMA CONTRACT CENTER 0.10
              sysdiff schema list [--json]
              sysdiff schema matrix [--json]
              sysdiff schema show <snapshot|comparison|bundle>
              sysdiff schema validate <snapshot|comparison|bundle> <file.json> [--json]
              sysdiff schema verify <snapshot|comparison|bundle> <file.json> [--json]

            Public contract v1 использует JSON Schema Draft 2020-12.
            Неизвестные additive properties разрешены. Обязательные поля, enum и версии проверяются.
            Breaking change требует нового schema major и отдельного migration guide.
            """);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
