using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using SysDiff.Domain;

namespace SysDiff.Storage;

public sealed class SqliteSnapshotStore : ISnapshotStore
{
    private readonly string _connectionString;
    private readonly JsonSerializerOptions _jsonOptions;

    public SqliteSnapshotStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        string? directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = Schema;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveSnapshotAsync(
        SnapshotRecord snapshot,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        using SqliteTransaction transaction = connection.BeginTransaction();

        await UpsertSnapshotAsync(connection, transaction, snapshot, cancellationToken);
        await DeleteSnapshotChildrenAsync(connection, transaction, snapshot.Id, cancellationToken);

        foreach (ProviderSnapshotResult result in snapshot.ProviderResults)
        {
            ProviderSnapshotResult compact = result with { Artifacts = [] };
            await InsertProviderResultAsync(
                connection,
                transaction,
                snapshot.Id,
                compact,
                cancellationToken);
        }

        foreach (SystemArtifact artifact in snapshot.Artifacts)
        {
            await InsertArtifactAsync(
                connection,
                transaction,
                snapshot.Id,
                artifact,
                cancellationToken);
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyList<SnapshotRecord>> ListSnapshotsAsync(
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, created_utc, version, schema_version, profile_name,
                   status, windows_edition, windows_build, architecture, comment
            FROM snapshots
            ORDER BY created_utc DESC;
            """;

        var result = new List<SnapshotRecord>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadSnapshotHeader(reader));
        }

        return result;
    }

    public async Task<SnapshotRecord?> GetSnapshotAsync(
        string nameOrId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameOrId);

        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, created_utc, version, schema_version, profile_name,
                   status, windows_edition, windows_build, architecture, comment
            FROM snapshots
            WHERE lower(name) = lower($value) OR id = $value
            ORDER BY created_utc DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$value", nameOrId.Trim());

        SnapshotRecord? snapshot = null;
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                snapshot = ReadSnapshotHeader(reader);
            }
        }

        if (snapshot is null)
        {
            return null;
        }

        List<ProviderSnapshotResult> providerResults =
            await LoadProviderResultsAsync(connection, snapshot.Id, cancellationToken);
        List<SystemArtifact> artifacts =
            await LoadArtifactsAsync(connection, snapshot.Id, cancellationToken);

        return snapshot with
        {
            ProviderResults = providerResults,
            Artifacts = artifacts
        };
    }

    public async Task DeleteSnapshotAsync(
        string nameOrId,
        CancellationToken cancellationToken)
    {
        SnapshotRecord? snapshot = await GetSnapshotAsync(nameOrId, cancellationToken);
        if (snapshot is null)
        {
            return;
        }

        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM snapshots WHERE id = $id;";
        command.Parameters.AddWithValue("$id", snapshot.Id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveComparisonAsync(
        ComparisonResult comparison,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        using SqliteTransaction transaction = connection.BeginTransaction();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO comparisons(
                    id, before_snapshot_id, after_snapshot_id, created_utc,
                    noise_mode, hidden_as_noise)
                VALUES($id, $before, $after, $created, $noise, $hidden)
                ON CONFLICT(id) DO UPDATE SET
                    before_snapshot_id = excluded.before_snapshot_id,
                    after_snapshot_id = excluded.after_snapshot_id,
                    created_utc = excluded.created_utc,
                    noise_mode = excluded.noise_mode,
                    hidden_as_noise = excluded.hidden_as_noise;
                DELETE FROM changes WHERE comparison_id = $id;
                """;
            command.Parameters.AddWithValue("$id", comparison.Id.ToString("D"));
            command.Parameters.AddWithValue("$before", comparison.BeforeSnapshotId.ToString("D"));
            command.Parameters.AddWithValue("$after", comparison.AfterSnapshotId.ToString("D"));
            command.Parameters.AddWithValue("$created", comparison.CreatedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$noise", comparison.NoiseMode.ToString());
            command.Parameters.AddWithValue("$hidden", comparison.HiddenAsNoise);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (SystemChange change in comparison.Changes)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO changes(id, comparison_id, change_json)
                VALUES($id, $comparison_id, $json);
                """;
            command.Parameters.AddWithValue("$id", change.Id.ToString("D"));
            command.Parameters.AddWithValue("$comparison_id", comparison.Id.ToString("D"));
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(change, _jsonOptions));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }

    public async Task<ComparisonResult?> GetComparisonAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT before_snapshot_id, after_snapshot_id, created_utc,
                   noise_mode, hidden_as_noise
            FROM comparisons
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        Guid beforeId;
        Guid afterId;
        DateTimeOffset created;
        NoiseMode noiseMode;
        int hidden;

        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            beforeId = Guid.Parse(reader.GetString(0));
            afterId = Guid.Parse(reader.GetString(1));
            created = DateTimeOffset.Parse(
                reader.GetString(2),
                System.Globalization.CultureInfo.InvariantCulture);
            noiseMode = Enum.Parse<NoiseMode>(reader.GetString(3), ignoreCase: true);
            hidden = reader.GetInt32(4);
        }

        var changes = new List<SystemChange>();
        await using SqliteCommand changesCommand = connection.CreateCommand();
        changesCommand.CommandText = """
            SELECT change_json
            FROM changes
            WHERE comparison_id = $id
            ORDER BY rowid;
            """;
        changesCommand.Parameters.AddWithValue("$id", id.ToString("D"));

        await using SqliteDataReader changesReader =
            await changesCommand.ExecuteReaderAsync(cancellationToken);

        while (await changesReader.ReadAsync(cancellationToken))
        {
            SystemChange? change = JsonSerializer.Deserialize<SystemChange>(
                changesReader.GetString(0),
                _jsonOptions);
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        return new ComparisonResult
        {
            Id = id,
            BeforeSnapshotId = beforeId,
            AfterSnapshotId = afterId,
            CreatedAtUtc = created,
            NoiseMode = noiseMode,
            HiddenAsNoise = hidden,
            Changes = changes
        };
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA foreign_keys = ON;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }

    private static async Task UpsertSnapshotAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SnapshotRecord snapshot,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO snapshots(
                id, name, created_utc, version, schema_version, profile_name,
                status, windows_edition, windows_build, architecture, comment)
            VALUES(
                $id, $name, $created, $version, $schema, $profile,
                $status, $edition, $build, $architecture, $comment)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                created_utc = excluded.created_utc,
                version = excluded.version,
                schema_version = excluded.schema_version,
                profile_name = excluded.profile_name,
                status = excluded.status,
                windows_edition = excluded.windows_edition,
                windows_build = excluded.windows_build,
                architecture = excluded.architecture,
                comment = excluded.comment;
            """;
        command.Parameters.AddWithValue("$id", snapshot.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", snapshot.Name);
        command.Parameters.AddWithValue("$created", snapshot.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$version", snapshot.SysDiffVersion);
        command.Parameters.AddWithValue("$schema", snapshot.SchemaVersion);
        command.Parameters.AddWithValue("$profile", snapshot.ProfileName);
        command.Parameters.AddWithValue("$status", snapshot.Status.ToString());
        command.Parameters.AddWithValue("$edition", (object?)snapshot.WindowsEdition ?? DBNull.Value);
        command.Parameters.AddWithValue("$build", (object?)snapshot.WindowsBuild ?? DBNull.Value);
        command.Parameters.AddWithValue("$architecture", snapshot.Architecture);
        command.Parameters.AddWithValue("$comment", (object?)snapshot.Comment ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteSnapshotChildrenAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid snapshotId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM snapshot_providers WHERE snapshot_id = $id;
            DELETE FROM artifacts WHERE snapshot_id = $id;
            """;
        command.Parameters.AddWithValue("$id", snapshotId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertProviderResultAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid snapshotId,
        ProviderSnapshotResult result,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO snapshot_providers(snapshot_id, provider_id, result_json)
            VALUES($snapshot_id, $provider_id, $json);
            """;
        command.Parameters.AddWithValue("$snapshot_id", snapshotId.ToString("D"));
        command.Parameters.AddWithValue("$provider_id", result.ProviderId);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(result, _jsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertArtifactAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid snapshotId,
        SystemArtifact artifact,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO artifacts(snapshot_id, identity, provider_id, artifact_json)
            VALUES($snapshot_id, $identity, $provider_id, $json);
            """;
        command.Parameters.AddWithValue("$snapshot_id", snapshotId.ToString("D"));
        command.Parameters.AddWithValue("$identity", artifact.Identity);
        command.Parameters.AddWithValue("$provider_id", artifact.ProviderId);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(artifact, _jsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<List<ProviderSnapshotResult>> LoadProviderResultsAsync(
        SqliteConnection connection,
        Guid snapshotId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT result_json
            FROM snapshot_providers
            WHERE snapshot_id = $id
            ORDER BY provider_id;
            """;
        command.Parameters.AddWithValue("$id", snapshotId.ToString("D"));

        var results = new List<ProviderSnapshotResult>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            ProviderSnapshotResult? result = JsonSerializer.Deserialize<ProviderSnapshotResult>(
                reader.GetString(0),
                _jsonOptions);
            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    private async Task<List<SystemArtifact>> LoadArtifactsAsync(
        SqliteConnection connection,
        Guid snapshotId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT artifact_json
            FROM artifacts
            WHERE snapshot_id = $id
            ORDER BY rowid;
            """;
        command.Parameters.AddWithValue("$id", snapshotId.ToString("D"));

        var artifacts = new List<SystemArtifact>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            SystemArtifact? artifact = JsonSerializer.Deserialize<SystemArtifact>(
                reader.GetString(0),
                _jsonOptions);
            if (artifact is not null)
            {
                artifacts.Add(artifact);
            }
        }

        return artifacts;
    }

    private static SnapshotRecord ReadSnapshotHeader(SqliteDataReader reader) =>
        new()
        {
            Id = Guid.Parse(reader.GetString(0)),
            Name = reader.GetString(1),
            CreatedAtUtc = DateTimeOffset.Parse(
                reader.GetString(2),
                System.Globalization.CultureInfo.InvariantCulture),
            SysDiffVersion = reader.GetString(3),
            SchemaVersion = reader.GetInt32(4),
            ProfileName = reader.GetString(5),
            Status = Enum.Parse<SnapshotStatus>(reader.GetString(6), ignoreCase: true),
            WindowsEdition = reader.IsDBNull(7) ? null : reader.GetString(7),
            WindowsBuild = reader.IsDBNull(8) ? null : reader.GetString(8),
            Architecture = reader.GetString(9),
            Comment = reader.IsDBNull(10) ? null : reader.GetString(10)
        };

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS snapshots(
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            created_utc TEXT NOT NULL,
            version TEXT NOT NULL,
            schema_version INTEGER NOT NULL,
            profile_name TEXT NOT NULL,
            status TEXT NOT NULL,
            windows_edition TEXT NULL,
            windows_build TEXT NULL,
            architecture TEXT NOT NULL,
            comment TEXT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_snapshots_name
            ON snapshots(name COLLATE NOCASE);

        CREATE TABLE IF NOT EXISTS snapshot_providers(
            snapshot_id TEXT NOT NULL,
            provider_id TEXT NOT NULL,
            result_json TEXT NOT NULL,
            PRIMARY KEY(snapshot_id, provider_id),
            FOREIGN KEY(snapshot_id) REFERENCES snapshots(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS artifacts(
            snapshot_id TEXT NOT NULL,
            identity TEXT NOT NULL,
            provider_id TEXT NOT NULL,
            artifact_json TEXT NOT NULL,
            PRIMARY KEY(snapshot_id, identity),
            FOREIGN KEY(snapshot_id) REFERENCES snapshots(id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS ix_artifacts_provider
            ON artifacts(snapshot_id, provider_id);

        CREATE TABLE IF NOT EXISTS comparisons(
            id TEXT PRIMARY KEY,
            before_snapshot_id TEXT NOT NULL,
            after_snapshot_id TEXT NOT NULL,
            created_utc TEXT NOT NULL,
            noise_mode TEXT NOT NULL,
            hidden_as_noise INTEGER NOT NULL,
            FOREIGN KEY(before_snapshot_id) REFERENCES snapshots(id) ON DELETE CASCADE,
            FOREIGN KEY(after_snapshot_id) REFERENCES snapshots(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS changes(
            id TEXT PRIMARY KEY,
            comparison_id TEXT NOT NULL,
            change_json TEXT NOT NULL,
            FOREIGN KEY(comparison_id) REFERENCES comparisons(id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS ix_changes_comparison
            ON changes(comparison_id);
        """;
}
