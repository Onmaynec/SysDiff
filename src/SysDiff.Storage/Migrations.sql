-- Схема версии 1. Файл продублирован для аудита и документации.
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

CREATE TABLE IF NOT EXISTS comparisons(
    id TEXT PRIMARY KEY,
    before_snapshot_id TEXT NOT NULL,
    after_snapshot_id TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    noise_mode TEXT NOT NULL,
    hidden_as_noise INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS changes(
    id TEXT PRIMARY KEY,
    comparison_id TEXT NOT NULL,
    change_json TEXT NOT NULL,
    FOREIGN KEY(comparison_id) REFERENCES comparisons(id) ON DELETE CASCADE
);
