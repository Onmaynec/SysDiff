using System.Text.Json;
using System.Text.Json.Serialization;
using SysDiff.Storage;

namespace SysDiff.Cli;

internal sealed class V9CommandRouter
{
    private readonly V8CommandRouter _v8;
    private readonly DatabaseMigrationService _migrations;

    public V9CommandRouter(
        V8CommandRouter v8,
        DatabaseMigrationService migrations)
    {
        _v8 = v8;
        _migrations = migrations;
    }

    public async Task<int> RunAsync(
        string[] args,
        CommandApp fallback,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            return await _v8.RunAsync(args, fallback, cancellationToken);
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

        if (args[0].Equals("migration", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("migrate", StringComparison.OrdinalIgnoreCase))
        {
            return await RunMigrationAsync(args, cancellationToken);
        }

        int result = await _v8.RunAsync(args, fallback, cancellationToken);
        if (args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
        }
        return result;
    }

    private async Task<int> RunMigrationAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        string command = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
        bool json = HasOption(args, "--json");

        switch (command)
        {
            case "status":
            case "plan":
            {
                DatabaseMigrationPlan plan = await _migrations.PlanAsync(cancellationToken);
                if (json)
                {
                    WriteJson(plan);
                }
                else
                {
                    PrintPlan(plan, detailed: command == "plan");
                }
                return plan.Status is DatabaseCompatibilityStatus.Invalid
                    or DatabaseCompatibilityStatus.RequiresNewerSysDiff
                        ? 9
                        : 0;
            }
            case "history":
            {
                DatabaseMigrationHistory history = await _migrations.GetHistoryAsync(cancellationToken);
                if (json)
                {
                    WriteJson(history);
                }
                else
                {
                    PrintHistory(history);
                }
                return 0;
            }
            case "apply":
            {
                if (!HasOption(args, "--yes"))
                {
                    throw new ArgumentException(
                        "Миграция требует явного подтверждения: migration apply --yes [--json]");
                }

                DatabaseMigrationResult result = await _migrations.ApplyAsync(cancellationToken);
                if (json)
                {
                    WriteJson(result);
                }
                else
                {
                    PrintResult(result);
                }
                return result.Success ? 0 : 9;
            }
            default:
                throw new ArgumentException(
                    "Команда migration: status, plan, history или apply.");
        }
    }

    private static void PrintPlan(DatabaseMigrationPlan plan, bool detailed)
    {
        Console.WriteLine("SysDiff Migration Lab");
        Console.WriteLine($"Database: {plan.DatabasePath}");
        Console.WriteLine($"Status: {plan.Status}");
        Console.WriteLine($"SQLite user_version: {plan.UserVersion}");
        Console.WriteLine($"Supported user_version: {plan.SupportedUserVersion}");
        Console.WriteLine($"Integrity: {(plan.IntegrityOk ? "ok" : plan.IntegrityMessage)}");
        Console.WriteLine($"Applied migrations: {plan.AppliedMigrations.Count}");
        Console.WriteLine($"Pending migrations: {plan.PendingMigrations.Count}");
        Console.WriteLine($"Automatic backup: {(plan.RequiresBackup ? "required" : "not required")}");
        Console.WriteLine(plan.Message);

        if (!detailed)
        {
            return;
        }

        foreach (DatabaseMigrationDescriptor migration in plan.PendingMigrations)
        {
            Console.WriteLine();
            Console.WriteLine($"[{migration.Id}] → {migration.TargetVersion}");
            Console.WriteLine($"  {migration.Description}");
            Console.WriteLine($"  destructive: {migration.Destructive}");
            Console.WriteLine($"  backup: {migration.RequiresBackup}");
        }

        foreach (string unknown in plan.UnknownAppliedMigrationIds)
        {
            Console.WriteLine($"Unknown applied migration: {unknown}");
        }
    }

    private static void PrintHistory(DatabaseMigrationHistory history)
    {
        Console.WriteLine("Database migration history");
        Console.WriteLine($"Database: {history.DatabasePath}");
        foreach (DatabaseMigrationHistoryEntry migration in history.AppliedMigrations)
        {
            Console.WriteLine(
                $"{migration.AppliedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}  " +
                $"{migration.Id}  known:{migration.Known}");
        }

        if (history.Runs.Count == 0)
        {
            Console.WriteLine("Migration runs: none");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Migration runs");
        foreach (DatabaseMigrationRunRecord run in history.Runs)
        {
            Console.WriteLine(
                $"{run.StartedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}  " +
                $"{run.Status,-7} {run.MigrationId}");
            if (!string.IsNullOrWhiteSpace(run.BackupPath))
            {
                Console.WriteLine($"  backup: {run.BackupPath}");
            }
            if (!string.IsNullOrWhiteSpace(run.Error))
            {
                Console.WriteLine($"  error: {run.Error}");
            }
        }
    }

    private static void PrintResult(DatabaseMigrationResult result)
    {
        Console.WriteLine(result.Success ? "Migration completed" : "Migration failed");
        Console.WriteLine(result.Message);
        Console.WriteLine($"Changed: {result.Changed}");
        if (!string.IsNullOrWhiteSpace(result.BackupPath))
        {
            Console.WriteLine($"Backup: {result.BackupPath}");
        }
        foreach (string id in result.AppliedMigrationIds)
        {
            Console.WriteLine($"Applied: {id}");
        }
        if (!string.IsNullOrWhiteSpace(result.FailedMigrationId))
        {
            Console.WriteLine($"Failed: {result.FailedMigrationId}");
        }
    }

    private static bool HasOption(IReadOnlyList<string> args, string option) =>
        args.Any(value => value.Equals(option, StringComparison.OrdinalIgnoreCase));

    private static void WriteJson<T>(T value) =>
        Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    private static void PrintSmokeFrame()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("SYSDIFF CYBER CONSOLE 0.9.0 // MIGRATION LAB");
        Console.WriteLine("[09] SYSTEM NODE > DATABASE PLAN | BACKUP | TRANSACTION | HISTORY");
        Console.WriteLine("SQLITE: GUARDED | DRY-RUN: DEFAULT | APPLY: CONFIRM | ROLLBACK: ATOMIC");
        Console.WriteLine("================================================================================");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """

            MIGRATION LAB 0.9
              sysdiff migration status [--json]
              sysdiff migration plan [--json]
              sysdiff migration history [--json]
              sysdiff migration apply --yes [--json]

            Plan является dry-run и не изменяет пользовательские данные.
            Apply всегда создаёт SQLite-consistent backup и применяет миграции транзакционно.
            База с более новым PRAGMA user_version открываться не будет.
            """);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
