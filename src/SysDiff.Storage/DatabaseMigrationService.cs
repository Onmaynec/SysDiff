using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SysDiff.Storage;

public sealed class DatabaseMigrationService
{
    public const string CurrentDatabaseVersion = "0.9.0";
    public const int CurrentUserVersion = 9;

    private static readonly HashSet<string> HistoricalMigrationIds =
        new(StringComparer.Ordinal)
        {
            "0.6.0-investigations"
        };

    private static readonly IReadOnlyList<DatabaseMigrationDefinition> DefaultMigrations =
    [
        new(
            new DatabaseMigrationDescriptor
            {
                Id = "0.9.0-migration-lab",
                TargetVersion = CurrentDatabaseVersion,
                UserVersion = CurrentUserVersion,
                Description = "Add transactional migration audit, database metadata and user_version guard",
                Destructive = false,
                RequiresBackup = true
            },
            """
            CREATE TABLE IF NOT EXISTS migration_runs(
                id TEXT PRIMARY KEY,
                migration_id TEXT NOT NULL,
                started_utc TEXT NOT NULL,
                finished_utc TEXT NULL,
                status TEXT NOT NULL,
                backup_path TEXT NULL,
                error TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_migration_runs_started
                ON migration_runs(started_utc DESC);

            CREATE INDEX IF NOT EXISTS ix_app_migrations_applied
                ON app_migrations(applied_utc DESC);

            CREATE TABLE IF NOT EXISTS database_metadata(
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            INSERT INTO database_metadata(key, value, updated_utc)
            VALUES('database.schema', '0.9', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                updated_utc = excluded.updated_utc;

            INSERT INTO database_metadata(key, value, updated_utc)
            VALUES('migration.engine', '1', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                updated_utc = excluded.updated_utc;

            PRAGMA user_version = 9;
            """)
    ];

    private readonly string _databasePath;
    private readonly string _backupDirectory;
    private readonly string _lockPath;
    private readonly IReadOnlyList<DatabaseMigrationDefinition> _migrations;

    public DatabaseMigrationService(string databasePath, string backupDirectory)
        : this(databasePath, backupDirectory, DefaultMigrations)
    {
    }

    internal DatabaseMigrationService(
        string databasePath,
        string backupDirectory,
        IReadOnlyList<DatabaseMigrationDefinition> migrations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);
        ArgumentNullException.ThrowIfNull(migrations);

        _databasePath = Path.GetFullPath(databasePath);
        _backupDirectory = Path.GetFullPath(backupDirectory);
        _lockPath = _databasePath + ".migration.lock";
        _migrations = migrations
            .OrderBy(value => value.Descriptor.UserVersion)
            .ThenBy(value => value.Descriptor.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task ValidateReadableAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_databasePath))
        {
            return;
        }

        await using SqliteConnection connection = await OpenAsync(
            SqliteOpenMode.ReadOnly,
            cancellationToken);
        int userVersion = await GetUserVersionAsync(connection, cancellationToken);
        if (userVersion > CurrentUserVersion)
        {
            throw new InvalidDataException(
                $"База использует SQLite user_version {userVersion}, " +
                $"но эта сборка поддерживает максимум {CurrentUserVersion}. Обновите SysDiff.");
        }

        string integrity = await QuickCheckAsync(connection, cancellationToken);
        if (!integrity.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"SQLite quick_check не пройден: {integrity}");
        }
    }

    public async Task<DatabaseMigrationPlan> PlanAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_databasePath))
        {
            return new DatabaseMigrationPlan
            {
                DatabasePath = _databasePath,
                DatabaseExists = false,
                UserVersion = 0,
                SupportedUserVersion = CurrentUserVersion,
                Status = DatabaseCompatibilityStatus.MigrationRequired,
                IntegrityOk = true,
                IntegrityMessage = "Database file does not exist yet.",
                PendingMigrations = _migrations.Select(value => value.Descriptor).ToList(),
                RequiresBackup = false,
                CanApply = false,
                Message = "Сначала запустите SysDiff, чтобы создать локальную базу."
            };
        }

        await using SqliteConnection connection = await OpenAsync(
            SqliteOpenMode.ReadOnly,
            cancellationToken);
        int userVersion = await GetUserVersionAsync(connection, cancellationToken);
        string integrity = await QuickCheckAsync(connection, cancellationToken);
        List<DatabaseMigrationHistoryEntry> applied = await ReadAppliedMigrationsAsync(
            connection,
            cancellationToken);
        HashSet<string> appliedIds = applied
            .Select(value => value.Id)
            .ToHashSet(StringComparer.Ordinal);
        List<DatabaseMigrationDescriptor> pending = _migrations
            .Where(value => !appliedIds.Contains(value.Descriptor.Id))
            .Select(value => value.Descriptor)
            .ToList();
        HashSet<string> knownIds = _migrations
            .Select(value => value.Descriptor.Id)
            .Concat(HistoricalMigrationIds)
            .ToHashSet(StringComparer.Ordinal);
        List<string> unknown = appliedIds
            .Where(id => !knownIds.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        if (!integrity.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            return CreatePlan(
                userVersion,
                integrity,
                applied,
                pending,
                unknown,
                DatabaseCompatibilityStatus.Invalid,
                canApply: false,
                "SQLite quick_check не пройден. Миграция заблокирована.");
        }

        if (userVersion > CurrentUserVersion || unknown.Count > 0)
        {
            string reason = userVersion > CurrentUserVersion
                ? $"База использует более новый user_version {userVersion}."
                : $"Найдены неизвестные migration IDs: {string.Join(", ", unknown)}.";
            return CreatePlan(
                userVersion,
                integrity,
                applied,
                pending,
                unknown,
                DatabaseCompatibilityStatus.RequiresNewerSysDiff,
                canApply: false,
                reason + " Текущая версия не будет изменять базу.");
        }

        if (pending.Count == 0 && userVersion < CurrentUserVersion)
        {
            return CreatePlan(
                userVersion,
                integrity,
                applied,
                pending,
                unknown,
                DatabaseCompatibilityStatus.Invalid,
                canApply: false,
                "Migration ledger и PRAGMA user_version не согласованы.");
        }

        if (pending.Count > 0)
        {
            return CreatePlan(
                userVersion,
                integrity,
                applied,
                pending,
                unknown,
                DatabaseCompatibilityStatus.MigrationRequired,
                canApply: true,
                $"Ожидают применения миграции: {pending.Count}.");
        }

        return CreatePlan(
            userVersion,
            integrity,
            applied,
            pending,
            unknown,
            DatabaseCompatibilityStatus.Current,
            canApply: false,
            "База соответствует текущей версии Migration Lab.");
    }

    public async Task<DatabaseMigrationHistory> GetHistoryAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_databasePath))
        {
            return new DatabaseMigrationHistory
            {
                DatabasePath = _databasePath
            };
        }

        await using SqliteConnection connection = await OpenAsync(
            SqliteOpenMode.ReadOnly,
            cancellationToken);
        return new DatabaseMigrationHistory
        {
            DatabasePath = _databasePath,
            AppliedMigrations = await ReadAppliedMigrationsAsync(connection, cancellationToken),
            Runs = await ReadRunsAsync(connection, cancellationToken)
        };
    }

    public Task<DatabaseMigrationResult> ApplyAsync(CancellationToken cancellationToken) =>
        ApplyCoreAsync(createBackup: true, cancellationToken);

    public Task<DatabaseMigrationResult> BootstrapNewDatabaseAsync(
        CancellationToken cancellationToken) =>
        ApplyCoreAsync(createBackup: false, cancellationToken);

    private async Task<DatabaseMigrationResult> ApplyCoreAsync(
        bool createBackup,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        if (!File.Exists(_databasePath))
        {
            return Failure(started, null, null, "Файл базы не найден.");
        }

        FileStream migrationLock;
        try
        {
            migrationLock = new FileStream(
                _lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException exception)
        {
            return Failure(
                started,
                null,
                null,
                $"Другая миграция уже выполняется: {exception.Message}");
        }

        DatabaseMigrationResult result;
        await using (migrationLock)
        {
            result = await ApplyLockedAsync(createBackup, started, cancellationToken);
        }

        try
        {
            File.Delete(_lockPath);
        }
        catch (IOException)
        {
        }

        return result;
    }

    private async Task<DatabaseMigrationResult> ApplyLockedAsync(
        bool createBackup,
        DateTimeOffset started,
        CancellationToken cancellationToken)
    {
        DatabaseMigrationPlan plan = await PlanAsync(cancellationToken);
        if (plan.PendingMigrations.Count == 0
            && plan.Status == DatabaseCompatibilityStatus.Current)
        {
            return new DatabaseMigrationResult
            {
                Success = true,
                Changed = false,
                StartedAtUtc = started,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Message = "Применять нечего: база уже актуальна."
            };
        }

        if (!plan.CanApply)
        {
            return Failure(started, null, null, plan.Message);
        }

        string? backupPath = createBackup
            ? await CreateBackupAsync(plan.PendingMigrations[0].Id, cancellationToken)
            : null;
        var appliedIds = new List<string>();

        foreach (DatabaseMigrationDescriptor descriptor in plan.PendingMigrations)
        {
            DatabaseMigrationDefinition definition = _migrations.Single(
                value => value.Descriptor.Id.Equals(descriptor.Id, StringComparison.Ordinal));
            Guid runId = Guid.NewGuid();
            DateTimeOffset migrationStarted = DateTimeOffset.UtcNow;

            try
            {
                await ApplyOneAsync(
                    definition,
                    runId,
                    migrationStarted,
                    backupPath,
                    cancellationToken);
                appliedIds.Add(descriptor.Id);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await TryRecordFailureAsync(
                    runId,
                    descriptor.Id,
                    migrationStarted,
                    backupPath,
                    exception.Message,
                    cancellationToken);
                return Failure(
                    started,
                    backupPath,
                    descriptor.Id,
                    $"Миграция {descriptor.Id} отменена транзакцией: {exception.Message}",
                    appliedIds);
            }
        }

        string integrity = await CheckIntegrityAsync(cancellationToken);
        if (!integrity.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            if (backupPath is not null)
            {
                await RestoreBackupAsync(backupPath, cancellationToken);
            }
            return Failure(
                started,
                backupPath,
                appliedIds.LastOrDefault(),
                backupPath is null
                    ? "После bootstrap SQLite quick_check не пройден."
                    : "После миграции SQLite quick_check не пройден; восстановлена резервная копия.",
                backupPath is null ? appliedIds : []);
        }

        return new DatabaseMigrationResult
        {
            Success = true,
            Changed = appliedIds.Count > 0,
            BackupPath = backupPath,
            AppliedMigrationIds = appliedIds,
            StartedAtUtc = started,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            Message = $"Успешно применено миграций: {appliedIds.Count}."
        };
    }

    private async Task ApplyOneAsync(
        DatabaseMigrationDefinition definition,
        Guid runId,
        DateTimeOffset started,
        string? backupPath,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(
            SqliteOpenMode.ReadWrite,
            cancellationToken);
        using SqliteTransaction transaction = connection.BeginTransaction();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = definition.Sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        DateTimeOffset finished = DateTimeOffset.UtcNow;
        await using (SqliteCommand history = connection.CreateCommand())
        {
            history.Transaction = transaction;
            history.CommandText = """
                INSERT INTO app_migrations(id, applied_utc, description)
                VALUES($id, $applied, $description)
                ON CONFLICT(id) DO NOTHING;
                """;
            history.Parameters.AddWithValue("$id", definition.Descriptor.Id);
            history.Parameters.AddWithValue("$applied", finished.ToString("O"));
            history.Parameters.AddWithValue("$description", definition.Descriptor.Description);
            await history.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (SqliteCommand run = connection.CreateCommand())
        {
            run.Transaction = transaction;
            run.CommandText = """
                INSERT INTO migration_runs(
                    id, migration_id, started_utc, finished_utc,
                    status, backup_path, error)
                VALUES($id, $migration, $started, $finished,
                       $status, $backup, NULL);
                """;
            run.Parameters.AddWithValue("$id", runId.ToString("D"));
            run.Parameters.AddWithValue("$migration", definition.Descriptor.Id);
            run.Parameters.AddWithValue("$started", started.ToString("O"));
            run.Parameters.AddWithValue("$finished", finished.ToString("O"));
            run.Parameters.AddWithValue("$status", DatabaseMigrationRunStatus.Applied.ToString());
            run.Parameters.AddWithValue("$backup", (object?)backupPath ?? DBNull.Value);
            await run.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }

    private DatabaseMigrationPlan CreatePlan(
        int userVersion,
        string integrity,
        List<DatabaseMigrationHistoryEntry> applied,
        List<DatabaseMigrationDescriptor> pending,
        List<string> unknown,
        DatabaseCompatibilityStatus status,
        bool canApply,
        string message) =>
        new()
        {
            DatabasePath = _databasePath,
            DatabaseExists = true,
            UserVersion = userVersion,
            SupportedUserVersion = CurrentUserVersion,
            Status = status,
            IntegrityOk = integrity.Equals("ok", StringComparison.OrdinalIgnoreCase),
            IntegrityMessage = integrity,
            AppliedMigrations = applied,
            PendingMigrations = pending,
            UnknownAppliedMigrationIds = unknown,
            RequiresBackup = pending.Any(value => value.RequiresBackup),
            CanApply = canApply,
            Message = message
        };

    private async Task<string> CreateBackupAsync(
        string migrationId,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_backupDirectory);
        string safeId = string.Concat(migrationId.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        string backupPath = Path.Combine(
            _backupDirectory,
            $"sysdiff-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-before-{safeId}.db");

        await using SqliteConnection source = await OpenAsync(
            SqliteOpenMode.ReadWrite,
            cancellationToken);
        await using (SqliteCommand checkpoint = source.CreateCommand())
        {
            checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await checkpoint.ExecuteNonQueryAsync(cancellationToken);
        }

        await using SqliteConnection destination = await OpenExternalAsync(
            backupPath,
            SqliteOpenMode.ReadWriteCreate,
            cancellationToken);
        source.BackupDatabase(destination);
        string integrity = await QuickCheckAsync(destination, cancellationToken);
        if (!integrity.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Резервная копия не прошла SQLite quick_check: {integrity}");
        }

        return backupPath;
    }

    private async Task RestoreBackupAsync(
        string backupPath,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection source = await OpenExternalAsync(
            backupPath,
            SqliteOpenMode.ReadOnly,
            cancellationToken);
        await using SqliteConnection destination = await OpenAsync(
            SqliteOpenMode.ReadWrite,
            cancellationToken);
        source.BackupDatabase(destination);
    }

    private async Task TryRecordFailureAsync(
        Guid runId,
        string migrationId,
        DateTimeOffset started,
        string? backupPath,
        string error,
        CancellationToken cancellationToken)
    {
        try
        {
            await using SqliteConnection connection = await OpenAsync(
                SqliteOpenMode.ReadWrite,
                cancellationToken);
            if (!await TableExistsAsync(connection, "migration_runs", cancellationToken))
            {
                return;
            }

            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO migration_runs(
                    id, migration_id, started_utc, finished_utc,
                    status, backup_path, error)
                VALUES($id, $migration, $started, $finished,
                       $status, $backup, $error);
                """;
            command.Parameters.AddWithValue("$id", runId.ToString("D"));
            command.Parameters.AddWithValue("$migration", migrationId);
            command.Parameters.AddWithValue("$started", started.ToString("O"));
            command.Parameters.AddWithValue("$finished", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$status", DatabaseMigrationRunStatus.Failed.ToString());
            command.Parameters.AddWithValue("$backup", (object?)backupPath ?? DBNull.Value);
            command.Parameters.AddWithValue("$error", error);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception) when (
            exception is SqliteException or IOException or InvalidOperationException)
        {
        }
    }

    private async Task<List<DatabaseMigrationHistoryEntry>> ReadAppliedMigrationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "app_migrations", cancellationToken))
        {
            return [];
        }

        HashSet<string> known = _migrations
            .Select(value => value.Descriptor.Id)
            .Concat(HistoricalMigrationIds)
            .ToHashSet(StringComparer.Ordinal);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, applied_utc, description
            FROM app_migrations
            ORDER BY applied_utc, id;
            """;
        var result = new List<DatabaseMigrationHistoryEntry>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string id = reader.GetString(0);
            result.Add(new DatabaseMigrationHistoryEntry
            {
                Id = id,
                AppliedAtUtc = ParseDate(reader.GetString(1)),
                Description = reader.GetString(2),
                Known = known.Contains(id)
            });
        }
        return result;
    }

    private static async Task<List<DatabaseMigrationRunRecord>> ReadRunsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "migration_runs", cancellationToken))
        {
            return [];
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, migration_id, started_utc, finished_utc,
                   status, backup_path, error
            FROM migration_runs
            ORDER BY started_utc DESC
            LIMIT 200;
            """;
        var result = new List<DatabaseMigrationRunRecord>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new DatabaseMigrationRunRecord
            {
                Id = Guid.Parse(reader.GetString(0)),
                MigrationId = reader.GetString(1),
                StartedAtUtc = ParseDate(reader.GetString(2)),
                FinishedAtUtc = reader.IsDBNull(3) ? null : ParseDate(reader.GetString(3)),
                Status = Enum.Parse<DatabaseMigrationRunStatus>(reader.GetString(4), ignoreCase: true),
                BackupPath = reader.IsDBNull(5) ? null : reader.GetString(5),
                Error = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }
        return result;
    }

    private Task<SqliteConnection> OpenAsync(
        SqliteOpenMode mode,
        CancellationToken cancellationToken) =>
        OpenExternalAsync(_databasePath, mode, cancellationToken);

    private static async Task<SqliteConnection> OpenExternalAsync(
        string path,
        SqliteOpenMode mode,
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Cache = SqliteCacheMode.Private,
            ForeignKeys = true,
            Pooling = false
        }.ToString());
        await connection.OpenAsync(cancellationToken);

        if (mode != SqliteOpenMode.ReadOnly)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA foreign_keys = ON;
                PRAGMA busy_timeout = 5000;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return connection;
    }

    private async Task<string> CheckIntegrityAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(
            SqliteOpenMode.ReadOnly,
            cancellationToken);
        return await QuickCheckAsync(connection, cancellationToken);
    }

    private static async Task<int> GetUserVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task<string> QuickCheckAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value?.ToString() ?? "quick_check returned no result";
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT 1
            FROM sqlite_master
            WHERE type = 'table' AND name = $name
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$name", table);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private static DatabaseMigrationResult Failure(
        DateTimeOffset started,
        string? backupPath,
        string? failedMigrationId,
        string message,
        IEnumerable<string>? appliedIds = null) =>
        new()
        {
            Success = false,
            Changed = appliedIds?.Any() == true,
            BackupPath = backupPath,
            AppliedMigrationIds = appliedIds?.ToList() ?? [],
            FailedMigrationId = failedMigrationId,
            StartedAtUtc = started,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            Message = message
        };
}
