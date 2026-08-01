using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using SysDiff.Domain;

namespace SysDiff.Storage;

public sealed class SqliteInvestigationStore : IInvestigationStore
{
    private const string BaselineKey = "investigation.baseline";
    private const string ActiveCaseKey = "investigation.activeCase";
    private readonly string _connectionString;
    private readonly JsonSerializerOptions _jsonOptions;

    public SqliteInvestigationStore(string databasePath)
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
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
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

    public async Task<BaselineRecord?> GetBaselineAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        string? json = await ReadSettingAsync(connection, BaselineKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        BaselineRecord? baseline = JsonSerializer.Deserialize<BaselineRecord>(json, _jsonOptions);
        if (baseline is null)
        {
            return null;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM snapshots WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", baseline.SnapshotId.ToString("D"));
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string name
            ? baseline with { SnapshotName = name }
            : null;
    }

    public async Task SetBaselineAsync(
        BaselineRecord baseline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand verify = connection.CreateCommand();
        verify.CommandText = "SELECT name FROM snapshots WHERE id = $id LIMIT 1;";
        verify.Parameters.AddWithValue("$id", baseline.SnapshotId.ToString("D"));
        object? value = await verify.ExecuteScalarAsync(cancellationToken);
        if (value is not string snapshotName)
        {
            throw new InvalidOperationException("Снимок baseline не найден в локальной базе.");
        }

        BaselineRecord normalized = baseline with
        {
            SnapshotName = snapshotName,
            SetAtUtc = baseline.SetAtUtc == default ? DateTimeOffset.UtcNow : baseline.SetAtUtc
        };
        await WriteSettingAsync(
            connection,
            BaselineKey,
            JsonSerializer.Serialize(normalized, _jsonOptions),
            cancellationToken);
    }

    public async Task ClearBaselineAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await DeleteSettingAsync(connection, BaselineKey, cancellationToken);
    }

    public async Task<InvestigationCaseRecord> CreateCaseAsync(
        InvestigationCaseRecord investigationCase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(investigationCase);
        string name = investigationCase.Name.Trim();
        if (name.Length == 0)
        {
            throw new ArgumentException("Название кейса не может быть пустым.", nameof(investigationCase));
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        InvestigationCaseRecord normalized = investigationCase with
        {
            Name = name,
            CreatedAtUtc = investigationCase.CreatedAtUtc == default ? now : investigationCase.CreatedAtUtc,
            UpdatedAtUtc = now,
            Tags = new HashSet<string>(
                investigationCase.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()),
                StringComparer.OrdinalIgnoreCase),
            Links = []
        };

        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await UpsertCaseAsync(connection, normalized, cancellationToken);
        await AppendTimelineAsync(new TimelineEventRecord
        {
            Kind = TimelineEventKind.Case,
            TimestampUtc = normalized.CreatedAtUtc,
            Title = $"Создан кейс: {normalized.Name}",
            ReferenceId = normalized.Id.ToString("D"),
            CaseId = normalized.Id,
            Status = normalized.Status.ToString()
        }, cancellationToken);
        return normalized;
    }

    public async Task UpdateCaseAsync(
        InvestigationCaseRecord investigationCase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(investigationCase);
        InvestigationCaseRecord normalized = investigationCase with
        {
            Name = investigationCase.Name.Trim(),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            ClosedAtUtc = investigationCase.Status == InvestigationCaseStatus.Closed
                ? investigationCase.ClosedAtUtc ?? DateTimeOffset.UtcNow
                : null
        };
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await UpsertCaseAsync(connection, normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<InvestigationCaseRecord>> ListCasesAsync(
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, description, tags_json, status, created_utc, updated_utc, closed_utc
            FROM investigation_cases
            ORDER BY CASE status WHEN 'Open' THEN 0 ELSE 1 END, updated_utc DESC;
            """;

        var result = new List<InvestigationCaseRecord>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadCase(reader));
        }

        return result;
    }

    public async Task<InvestigationCaseRecord?> GetCaseAsync(
        string nameOrId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameOrId);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, description, tags_json, status, created_utc, updated_utc, closed_utc
            FROM investigation_cases
            WHERE id = $value OR lower(name) = lower($value)
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$value", nameOrId.Trim());

        InvestigationCaseRecord? result = null;
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                result = ReadCase(reader);
            }
        }

        if (result is null)
        {
            return null;
        }

        List<InvestigationLink> links = await LoadLinksAsync(connection, result.Id, cancellationToken);
        return result with { Links = links };
    }

    public async Task SetActiveCaseAsync(Guid? caseId, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        if (caseId is null)
        {
            await DeleteSettingAsync(connection, ActiveCaseKey, cancellationToken);
            return;
        }

        await using SqliteCommand verify = connection.CreateCommand();
        verify.CommandText = "SELECT status FROM investigation_cases WHERE id = $id LIMIT 1;";
        verify.Parameters.AddWithValue("$id", caseId.Value.ToString("D"));
        object? status = await verify.ExecuteScalarAsync(cancellationToken);
        if (status is not string statusText)
        {
            throw new InvalidOperationException("Кейс не найден.");
        }
        if (Enum.Parse<InvestigationCaseStatus>(statusText, ignoreCase: true) == InvestigationCaseStatus.Closed)
        {
            throw new InvalidOperationException("Закрытый кейс нельзя сделать активным.");
        }

        await WriteSettingAsync(connection, ActiveCaseKey, caseId.Value.ToString("D"), cancellationToken);
    }

    public async Task<InvestigationCaseRecord?> GetActiveCaseAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        string? id = await ReadSettingAsync(connection, ActiveCaseKey, cancellationToken);
        return string.IsNullOrWhiteSpace(id)
            ? null
            : await GetCaseAsync(id, cancellationToken);
    }

    public async Task LinkAsync(
        Guid caseId,
        InvestigationLink link,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        using SqliteTransaction transaction = connection.BeginTransaction();
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO investigation_links(case_id, kind, reference_id, display_name, linked_utc)
                VALUES($case, $kind, $reference, $display, $linked)
                ON CONFLICT(case_id, kind, reference_id) DO UPDATE SET
                    display_name = excluded.display_name,
                    linked_utc = excluded.linked_utc;
                """;
            command.Parameters.AddWithValue("$case", caseId.ToString("D"));
            command.Parameters.AddWithValue("$kind", link.Kind);
            command.Parameters.AddWithValue("$reference", link.ReferenceId);
            command.Parameters.AddWithValue("$display", link.DisplayName);
            command.Parameters.AddWithValue("$linked", link.LinkedAtUtc.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (SqliteCommand update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE investigation_cases SET updated_utc = $updated WHERE id = $id;";
            update.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$id", caseId.ToString("D"));
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        transaction.Commit();
    }

    public async Task AppendTimelineAsync(
        TimelineEventRecord timelineEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timelineEvent);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO timeline_events(
                id, kind, timestamp_utc, title, reference_id, case_id, severity, status, metadata_json)
            VALUES($id, $kind, $timestamp, $title, $reference, $case, $severity, $status, $metadata)
            ON CONFLICT(id) DO UPDATE SET
                kind = excluded.kind,
                timestamp_utc = excluded.timestamp_utc,
                title = excluded.title,
                reference_id = excluded.reference_id,
                case_id = excluded.case_id,
                severity = excluded.severity,
                status = excluded.status,
                metadata_json = excluded.metadata_json;
            """;
        command.Parameters.AddWithValue("$id", timelineEvent.Id.ToString("D"));
        command.Parameters.AddWithValue("$kind", timelineEvent.Kind.ToString());
        command.Parameters.AddWithValue("$timestamp", timelineEvent.TimestampUtc.ToString("O"));
        command.Parameters.AddWithValue("$title", timelineEvent.Title);
        command.Parameters.AddWithValue("$reference", (object?)timelineEvent.ReferenceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$case", timelineEvent.CaseId?.ToString("D") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$severity", timelineEvent.Severity?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$status", timelineEvent.Status);
        command.Parameters.AddWithValue("$metadata", JsonSerializer.Serialize(timelineEvent.Metadata, _jsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimelineEventRecord>> ListTimelineAsync(
        int limit,
        TimelineEventKind? kind,
        CancellationToken cancellationToken)
    {
        int safeLimit = Math.Clamp(limit, 1, 1000);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        var events = new List<TimelineEventRecord>();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT id, kind, timestamp_utc, title, reference_id, case_id, severity, status, metadata_json
                FROM timeline_events
                ORDER BY timestamp_utc DESC, id DESC;
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(ReadTimeline(reader));
            }
        }

        await AddLegacySnapshotEventsAsync(connection, events, cancellationToken);
        await AddLegacyComparisonEventsAsync(connection, events, cancellationToken);

        IEnumerable<TimelineEventRecord> filtered = events
            .GroupBy(value => $"{value.Kind}:{value.ReferenceId ?? value.Id.ToString("D")}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(value => value.TimestampUtc).First())
            .OrderByDescending(value => value.TimestampUtc)
            .ThenByDescending(value => value.Id);
        if (kind is not null)
        {
            filtered = filtered.Where(value => value.Kind == kind.Value);
        }
        return filtered.Take(safeLimit).ToArray();
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

    private async Task UpsertCaseAsync(
        SqliteConnection connection,
        InvestigationCaseRecord investigationCase,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO investigation_cases(
                id, name, description, tags_json, status, created_utc, updated_utc, closed_utc)
            VALUES($id, $name, $description, $tags, $status, $created, $updated, $closed)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                description = excluded.description,
                tags_json = excluded.tags_json,
                status = excluded.status,
                updated_utc = excluded.updated_utc,
                closed_utc = excluded.closed_utc;
            """;
        command.Parameters.AddWithValue("$id", investigationCase.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", investigationCase.Name);
        command.Parameters.AddWithValue("$description", investigationCase.Description);
        command.Parameters.AddWithValue("$tags", JsonSerializer.Serialize(investigationCase.Tags, _jsonOptions));
        command.Parameters.AddWithValue("$status", investigationCase.Status.ToString());
        command.Parameters.AddWithValue("$created", investigationCase.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$updated", investigationCase.UpdatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$closed", investigationCase.ClosedAtUtc?.ToString("O") ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<List<InvestigationLink>> LoadLinksAsync(
        SqliteConnection connection,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT kind, reference_id, display_name, linked_utc
            FROM investigation_links
            WHERE case_id = $case
            ORDER BY linked_utc DESC;
            """;
        command.Parameters.AddWithValue("$case", caseId.ToString("D"));
        var links = new List<InvestigationLink>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            links.Add(new InvestigationLink
            {
                Kind = reader.GetString(0),
                ReferenceId = reader.GetString(1),
                DisplayName = reader.GetString(2),
                LinkedAtUtc = ParseDate(reader.GetString(3))
            });
        }
        return links;
    }

    private async Task AddLegacySnapshotEventsAsync(
        SqliteConnection connection,
        ICollection<TimelineEventRecord> events,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, created_utc, status, profile_name FROM snapshots;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string id = reader.GetString(0);
            events.Add(new TimelineEventRecord
            {
                Id = DeterministicId("snapshot", id),
                Kind = TimelineEventKind.Snapshot,
                TimestampUtc = ParseDate(reader.GetString(2)),
                Title = $"Snapshot: {reader.GetString(1)}",
                ReferenceId = id,
                Status = reader.GetString(3),
                Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["profile"] = reader.GetString(4)
                }
            });
        }
    }

    private async Task AddLegacyComparisonEventsAsync(
        SqliteConnection connection,
        ICollection<TimelineEventRecord> events,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.id, c.created_utc, c.noise_mode, b.name, a.name
            FROM comparisons c
            LEFT JOIN snapshots b ON b.id = c.before_snapshot_id
            LEFT JOIN snapshots a ON a.id = c.after_snapshot_id;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string id = reader.GetString(0);
            string before = reader.IsDBNull(3) ? "unknown" : reader.GetString(3);
            string after = reader.IsDBNull(4) ? "unknown" : reader.GetString(4);
            events.Add(new TimelineEventRecord
            {
                Id = DeterministicId("comparison", id),
                Kind = TimelineEventKind.Comparison,
                TimestampUtc = ParseDate(reader.GetString(1)),
                Title = $"Comparison: {before} → {after}",
                ReferenceId = id,
                Status = "Completed",
                Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["noise"] = reader.GetString(2)
                }
            });
        }
    }

    private static InvestigationCaseRecord ReadCase(SqliteDataReader reader)
    {
        HashSet<string>? tags = JsonSerializer.Deserialize<HashSet<string>>(
            reader.GetString(3),
            SharedJsonOptions);
        return new InvestigationCaseRecord
        {
            Id = Guid.Parse(reader.GetString(0)),
            Name = reader.GetString(1),
            Description = reader.GetString(2),
            Tags = tags ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Status = Enum.Parse<InvestigationCaseStatus>(reader.GetString(4), ignoreCase: true),
            CreatedAtUtc = ParseDate(reader.GetString(5)),
            UpdatedAtUtc = ParseDate(reader.GetString(6)),
            ClosedAtUtc = reader.IsDBNull(7) ? null : ParseDate(reader.GetString(7))
        };
    }

    private TimelineEventRecord ReadTimeline(SqliteDataReader reader)
    {
        Dictionary<string, string?>? metadata = JsonSerializer.Deserialize<Dictionary<string, string?>>(
            reader.GetString(8),
            _jsonOptions);
        return new TimelineEventRecord
        {
            Id = Guid.Parse(reader.GetString(0)),
            Kind = Enum.Parse<TimelineEventKind>(reader.GetString(1), ignoreCase: true),
            TimestampUtc = ParseDate(reader.GetString(2)),
            Title = reader.GetString(3),
            ReferenceId = reader.IsDBNull(4) ? null : reader.GetString(4),
            CaseId = reader.IsDBNull(5) ? null : Guid.Parse(reader.GetString(5)),
            Severity = reader.IsDBNull(6) ? null : Enum.Parse<Severity>(reader.GetString(6), ignoreCase: true),
            Status = reader.GetString(7),
            Metadata = metadata ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static async Task<string?> ReadSettingAsync(
        SqliteConnection connection,
        string key,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM investigation_settings WHERE key = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", key);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value as string;
    }

    private static async Task WriteSettingAsync(
        SqliteConnection connection,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO investigation_settings(key, value, updated_utc)
            VALUES($key, $value, $updated)
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteSettingAsync(
        SqliteConnection connection,
        string key,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM investigation_settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static Guid DeterministicId(string scope, string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"sysdiff:{scope}:{value}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static readonly JsonSerializerOptions SharedJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS app_migrations(
            id TEXT PRIMARY KEY,
            applied_utc TEXT NOT NULL,
            description TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS investigation_settings(
            key TEXT PRIMARY KEY,
            value TEXT NULL,
            updated_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS investigation_cases(
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            description TEXT NOT NULL,
            tags_json TEXT NOT NULL,
            status TEXT NOT NULL,
            created_utc TEXT NOT NULL,
            updated_utc TEXT NOT NULL,
            closed_utc TEXT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ix_investigation_cases_name
            ON investigation_cases(name COLLATE NOCASE);

        CREATE TABLE IF NOT EXISTS investigation_links(
            case_id TEXT NOT NULL,
            kind TEXT NOT NULL,
            reference_id TEXT NOT NULL,
            display_name TEXT NOT NULL,
            linked_utc TEXT NOT NULL,
            PRIMARY KEY(case_id, kind, reference_id),
            FOREIGN KEY(case_id) REFERENCES investigation_cases(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS timeline_events(
            id TEXT PRIMARY KEY,
            kind TEXT NOT NULL,
            timestamp_utc TEXT NOT NULL,
            title TEXT NOT NULL,
            reference_id TEXT NULL,
            case_id TEXT NULL,
            severity TEXT NULL,
            status TEXT NOT NULL,
            metadata_json TEXT NOT NULL,
            FOREIGN KEY(case_id) REFERENCES investigation_cases(id) ON DELETE SET NULL
        );

        CREATE INDEX IF NOT EXISTS ix_timeline_events_timestamp
            ON timeline_events(timestamp_utc DESC);

        CREATE INDEX IF NOT EXISTS ix_timeline_events_case
            ON timeline_events(case_id, timestamp_utc DESC);

        INSERT OR IGNORE INTO app_migrations(id, applied_utc, description)
        VALUES('0.6.0-investigations', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
               'Additive baseline, investigation cases and timeline schema');
        """;
}
