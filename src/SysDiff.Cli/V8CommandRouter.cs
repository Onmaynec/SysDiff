using System.Text.Json;
using System.Text.Json.Serialization;
using SysDiff.Storage;

namespace SysDiff.Cli;

internal sealed class V8CommandRouter
{
    private readonly V7CommandRouter _v7;
    private readonly SnapshotArchiveService _archives;

    public V8CommandRouter(V7CommandRouter v7, SnapshotArchiveService archives)
    {
        _v7 = v7;
        _archives = archives;
    }

    public async Task<int> RunAsync(
        string[] args,
        CommandApp fallback,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            return await _v7.RunAsync(args, fallback, cancellationToken);
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

        if (args[0].Equals("compatibility", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("compat", StringComparison.OrdinalIgnoreCase))
        {
            return await RunCompatibilityAsync(args, cancellationToken);
        }

        int result = await _v7.RunAsync(args, fallback, cancellationToken);
        if (args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
        }
        return result;
    }

    private async Task<int> RunCompatibilityAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        string command = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
        bool json = HasOption(args, "--json");

        switch (command)
        {
            case "status":
            case "matrix":
            {
                var status = new CompatibilityStatus(
                    ProductVersion: ProductInfo.Version,
                    SnapshotArchiveFormat: SnapshotArchiveCompatibility.FormatName,
                    CurrentFormatVersion: SnapshotArchiveCompatibility.CurrentFormatVersion,
                    MinimumReadableFormatVersion: SnapshotArchiveCompatibility.MinimumReadableFormatVersion,
                    CurrentSchemaVersion: SnapshotArchiveCompatibility.CurrentSchemaVersion,
                    MinimumReadableSchemaVersion: SnapshotArchiveCompatibility.MinimumReadableSchemaVersion,
                    Policy: "newer formats are rejected; legacy formats require an explicit migration path");
                if (json)
                {
                    WriteJson(status);
                }
                else
                {
                    Console.WriteLine("SysDiff Compatibility Center");
                    Console.WriteLine($"Product: {status.ProductVersion}");
                    Console.WriteLine($"Format: {status.SnapshotArchiveFormat} v{status.CurrentFormatVersion}");
                    Console.WriteLine($"Readable format versions: {status.MinimumReadableFormatVersion}..{status.CurrentFormatVersion}");
                    Console.WriteLine($"Readable schema versions: {status.MinimumReadableSchemaVersion}..{status.CurrentSchemaVersion}");
                    Console.WriteLine("Policy: newer archives are never partially imported.");
                }
                return 0;
            }
            case "inspect":
            case "verify":
            {
                if (args.Length < 3)
                {
                    throw new ArgumentException(
                        "Использование: compatibility inspect <file.sdshot> [--json]");
                }

                SnapshotArchiveInspection inspection = await _archives.InspectAsync(
                    args[2],
                    cancellationToken);
                if (json)
                {
                    WriteJson(inspection);
                }
                else
                {
                    PrintInspection(inspection);
                }
                return inspection.CanImport ? 0 : 4;
            }
            default:
                throw new ArgumentException(
                    "Команда compatibility: status, matrix, inspect или verify.");
        }
    }

    private static void PrintInspection(SnapshotArchiveInspection inspection)
    {
        Console.WriteLine("Snapshot compatibility inspection");
        Console.WriteLine($"Path: {inspection.ArchivePath}");
        Console.WriteLine($"Status: {inspection.Status}");
        Console.WriteLine($"Format: {inspection.Format ?? "-"} v{inspection.FormatVersion?.ToString() ?? "-"}");
        Console.WriteLine($"Schema: {inspection.SchemaVersion?.ToString() ?? "-"}");
        Console.WriteLine($"Producer: {inspection.ProducerVersion ?? "-"}");
        Console.WriteLine($"Snapshot: {inspection.SnapshotId?.ToString("D") ?? "-"}");
        Console.WriteLine($"Checksums: {(inspection.ChecksumsValid ? "valid" : "invalid")}");
        Console.WriteLine($"Can import: {inspection.CanImport}");
        Console.WriteLine(inspection.Message);
        foreach (string warning in inspection.Warnings)
        {
            Console.WriteLine($"Warning: {warning}");
        }
    }

    private static bool HasOption(IReadOnlyList<string> args, string option) =>
        args.Any(value => value.Equals(option, StringComparison.OrdinalIgnoreCase));

    private static void WriteJson<T>(T value) =>
        Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    private static void PrintSmokeFrame()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("SYSDIFF CYBER CONSOLE 0.8.0 // COMPATIBILITY CENTER");
        Console.WriteLine("[03] DRIFT OPS  [05] CASE VAULT  [09] SYSTEM NODE > FORMAT MATRIX");
        Console.WriteLine("SDSHOT: VERIFIED | SCHEMA: GUARDED | NEWER FORMAT: REJECT | IMPORT: ATOMIC");
        Console.WriteLine("================================================================================");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """

            COMPATIBILITY CENTER 0.8
              sysdiff compatibility status [--json]
              sysdiff compatibility matrix [--json]
              sysdiff compatibility inspect <file.sdshot> [--json]
              sysdiff compatibility verify <file.sdshot> [--json]

            Inspect проверяет ZIP-структуру, manifest, schema, Snapshot ID и SHA-256
            без записи данных в SQLite. Более новые и неизвестные схемы не импортируются частично.
            """);
    }

    private sealed record CompatibilityStatus(
        string ProductVersion,
        string SnapshotArchiveFormat,
        int CurrentFormatVersion,
        int MinimumReadableFormatVersion,
        int CurrentSchemaVersion,
        int MinimumReadableSchemaVersion,
        string Policy);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
