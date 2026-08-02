using Microsoft.Data.Sqlite;
using SysDiff.Domain;
using SysDiff.Storage;

namespace SysDiff.Core.Tests;

public sealed class DatabaseMigrationServiceTests
{
    [Fact]
    public async Task Apply_CreatesBackupAndIsIdempotent()
    {
        string root = CreateTemporaryDirectory();
        string databasePath = Path.Combine(root, "sysdiff.db");
        string backupDirectory = Path.Combine(root, "backups");

        try
        {
            SqliteSnapshotStore snapshotStore = await InitializeDatabaseAsync(databasePath);
            var snapshot = new SnapshotRecord
            {
                Name = "migration-source",
                Status = SnapshotStatus.Completed
            };
            await snapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None);

            var service = new DatabaseMigrationService(databasePath, backupDirectory);
            DatabaseMigrationPlan before = await service.PlanAsync(CancellationToken.None);

            Assert.Equal(DatabaseCompatibilityStatus.MigrationRequired, before.Status);
            Assert.Equal(0, before.UserVersion);
            Assert.Single(before.PendingMigrations);
            Assert.True(before.CanApply);

            DatabaseMigrationResult result = await service.ApplyAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.Changed);
            Assert.NotNull(result.BackupPath);
            Assert.True(File.Exists(result.BackupPath));
            Assert.Contains("0.9.0-migration-lab", result.AppliedMigrationIds);

            DatabaseMigrationPlan after = await service.PlanAsync(CancellationToken.None);
            Assert.Equal(DatabaseCompatibilityStatus.Current, after.Status);
            Assert.Equal(DatabaseMigrationService.CurrentUserVersion, after.UserVersion);
            Assert.Empty(after.PendingMigrations);

            DatabaseMigrationResult repeated = await service.ApplyAsync(CancellationToken.None);
            Assert.True(repeated.Success);
            Assert.False(repeated.Changed);
            Assert.Null(repeated.BackupPath);

            var backupStore = new SqliteSnapshotStore(result.BackupPath!);
            SnapshotRecord? backupSnapshot = await backupStore.GetSnapshotAsync(
                snapshot.Id.ToString("D"),
                CancellationToken.None);
            Assert.NotNull(backupSnapshot);

            DatabaseMigrationHistory history = await service.GetHistoryAsync(CancellationToken.None);
            Assert.Contains(history.AppliedMigrations, value => value.Id == "0.9.0-migration-lab");
            Assert.Contains(history.Runs, value =>
                value.MigrationId == "0.9.0-migration-lab"
                && value.Status == DatabaseMigrationRunStatus.Applied);
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task BootstrapNewDatabase_AppliesWithoutBackup()
    {
        string root = CreateTemporaryDirectory();
        string databasePath = Path.Combine(root, "sysdiff.db");
        string backupDirectory = Path.Combine(root, "backups");

        try
        {
            await InitializeDatabaseAsync(databasePath);
            var service = new DatabaseMigrationService(databasePath, backupDirectory);

            DatabaseMigrationResult result = await service.BootstrapNewDatabaseAsync(
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.Changed);
            Assert.Null(result.BackupPath);
            Assert.False(Directory.Exists(backupDirectory));

            DatabaseMigrationPlan plan = await service.PlanAsync(CancellationToken.None);
            Assert.Equal(DatabaseCompatibilityStatus.Current, plan.Status);
            Assert.Equal(DatabaseMigrationService.CurrentUserVersion, plan.UserVersion);
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task Apply_FailingMigrationRollsBackTransaction()
    {
        string root = CreateTemporaryDirectory();
        string databasePath = Path.Combine(root, "sysdiff.db");
        string backupDirectory = Path.Combine(root, "backups");

        try
        {
            await InitializeDatabaseAsync(databasePath);
            var definition = new DatabaseMigrationDefinition(
                new DatabaseMigrationDescriptor
                {
                    Id = "0.9.0-failing-test",
                    TargetVersion = "0.9.0",
                    UserVersion = 9,
                    Description = "Failure injection migration"
                },
                """
                CREATE TABLE migration_runs(
                    id TEXT PRIMARY KEY,
                    migration_id TEXT NOT NULL,
                    started_utc TEXT NOT NULL,
                    finished_utc TEXT NULL,
                    status TEXT NOT NULL,
                    backup_path TEXT NULL,
                    error TEXT NULL
                );
                CREATE TABLE rollback_probe(id TEXT PRIMARY KEY);
                INSERT INTO rollback_probe(id) VALUES('created-inside-transaction');
                SELECT sysdiff_missing_function();
                """);
            var service = new DatabaseMigrationService(
                databasePath,
                backupDirectory,
                [definition]);

            DatabaseMigrationResult result = await service.ApplyAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("0.9.0-failing-test", result.FailedMigrationId);
            Assert.NotNull(result.BackupPath);
            Assert.True(File.Exists(result.BackupPath));

            await using var connection = new SqliteConnection(
                $"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync();
            await using SqliteCommand probe = connection.CreateCommand();
            probe.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table' AND name = 'rollback_probe';
                """;
            Assert.Equal(0L, (long)(await probe.ExecuteScalarAsync())!);

            await using SqliteCommand history = connection.CreateCommand();
            history.CommandText = """
                SELECT COUNT(*)
                FROM app_migrations
                WHERE id = '0.9.0-failing-test';
                """;
            Assert.Equal(0L, (long)(await history.ExecuteScalarAsync())!);
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task ValidateReadable_RejectsFutureUserVersion()
    {
        string root = CreateTemporaryDirectory();
        string databasePath = Path.Combine(root, "sysdiff.db");

        try
        {
            await InitializeDatabaseAsync(databasePath);
            await using (var connection = new SqliteConnection(
                $"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "PRAGMA user_version = 10;";
                await command.ExecuteNonQueryAsync();
            }

            var service = new DatabaseMigrationService(
                databasePath,
                Path.Combine(root, "backups"));

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.ValidateReadableAsync(CancellationToken.None));
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    private static async Task<SqliteSnapshotStore> InitializeDatabaseAsync(string databasePath)
    {
        var snapshotStore = new SqliteSnapshotStore(databasePath);
        await snapshotStore.InitializeAsync(CancellationToken.None);
        var investigationStore = new SqliteInvestigationStore(databasePath);
        await investigationStore.InitializeAsync(CancellationToken.None);
        return snapshotStore;
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "SysDiff.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string root)
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }
}
