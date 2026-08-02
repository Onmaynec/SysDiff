# 🏗️ Архитектура SysDiff 0.9.0

## Цель

SysDiff разделяет сбор Windows-данных, comparison, investigations, хранение, отчёты, terminal UI, release channel, portable compatibility и database migrations. Интерактивный Cyber Console и non-interactive CLI используют одинаковые application services.

## Слои

```text
Cyber Control Node                         Non-interactive CLI
        │                                           │
        └────────────────────┬──────────────────────┘
                             ▼
              V9 → V8 → V7 → V6 → V4 → V3
                             │
       ┌─────────────────────┼──────────────────────────┐
       ▼                     ▼                          ▼
Snapshot workflows      Drift Operations          Data Safety
       │                     │                          │
       ▼                     ├─ ComparisonEngine        ├─ Compatibility Center
SnapshotCoordinator         ├─ DriftRiskEngine         ├─ SnapshotArchiveService
       │                     ├─ Reporting               └─ DatabaseMigrationService
       ▼                     └─ Timeline / Cases                 │
ISnapshotProvider[]                  │                           ├─ plan/history
       │                             │                           ├─ backup/lock
       └──────────────┬──────────────┘                           └─ transaction/verify
                      ▼
             ISnapshotStore / IInvestigationStore
                      │
                      ▼
                 SQLite sysdiff.db
```

## Проекты solution

| Проект | Ответственность |
|---|---|
| `SysDiff.Domain` | records, enums и storage contracts |
| `SysDiff.Core` | capture coordination, comparison, profiles, risk и privacy |
| `SysDiff.Storage` | SQLite, migrations, `.sdshot`, compatibility inspection |
| `SysDiff.Providers` | read-only Windows data providers |
| `SysDiff.Reporting` | Console, JSON, Markdown и HTML |
| `SysDiff.ProviderSdk` | явный контракт внешних providers |
| `SysDiff.Cli` | DI, command routers, TUI, watch/live, updater |

## Versioned command routers

```text
V9CommandRouter
  ├── migration status|plan|history|apply
  └── V8CommandRouter
        ├── compatibility status|matrix|inspect|verify
        └── V7CommandRouter
              ├── update check|status|download|install|settings|clear-cache
              └── V6CommandRouter
                    ├── baseline
                    ├── drift
                    ├── timeline
                    ├── case
                    └── V4 → V3 → CommandApp
```

Новая версия перехватывает только свои команды и делегирует остальные вниз. Это сохраняет automation scripts и снижает риск дублирования parser logic.

## Database Migration Service

`DatabaseMigrationService` расположен в Storage и не зависит от CLI/TUI. Он получает путь базы и каталог backup.

### Startup guard

До `ISnapshotStore.InitializeAsync` выполняется `ValidateReadableAsync`:

1. если базы нет — проверка завершается;
2. открывается private non-pooled read-only connection;
3. читается `PRAGMA user_version`;
4. версия выше `CurrentUserVersion` отклоняется;
5. выполняется `PRAGMA quick_check`.

Это предотвращает запись старой сборкой в более новую базу.

### Plan

`PlanAsync` является read-only и объединяет:

- `PRAGMA user_version`;
- `PRAGMA quick_check`;
- `app_migrations`;
- известные migration definitions;
- unknown migration IDs;
- pending steps и backup requirement.

Результат: `Current`, `MigrationRequired`, `RequiresNewerSysDiff` или `Invalid`.

### Apply

```text
exclusive lock
      ↓
read-only plan
      ↓
WAL checkpoint
      ↓
SQLite Backup API + quick_check
      ↓
BEGIN transaction
      ↓
migration SQL
      ↓
app_migrations + migration_runs
      ↓
COMMIT
      ↓
post-migration quick_check
```

Каждая definition выполняется в своей transaction. SQL failure откатывает schema changes и ledger row вместе. Если post-commit integrity нарушена, существующая база восстанавливается из backup.

### Новая база

`Program` запоминает существование файла до store initialization. Для новой базы после создания core/investigation tables вызывается `BootstrapNewDatabaseAsync`; текущий ledger применяется без backup. Существующая база не мигрируется автоматически.

### Migration 0.9

`0.9.0-migration-lab` добавляет:

```text
migration_runs
  id, migration_id, started_utc, finished_utc,
  status, backup_path, error

database_metadata
  key, value, updated_utc
```

Также создаются history indexes и устанавливается `PRAGMA user_version = 9`. Core snapshot/comparison/investigation tables не переписываются.

## Snapshot Archive Compatibility

`SnapshotArchiveService.InspectAsync` проверяет `.sdshot` без записи:

1. file/entry size limits;
2. все ZIP paths;
3. единственные обязательные entries;
4. точные SHA-256 строки;
5. JSON parsing;
6. format, Snapshot ID и schema invariants;
7. compatibility policy.

`ImportAsync` сохраняет snapshot только при `CanImport=true`.

Текущая reader matrix:

```text
container format: 1..1
snapshot schema:  1..1
```

Она ещё не является stable public schema 1.0.

## Release Channel

`UpdateService` проверяет официальный stable manifest, HTTPS allow-list, size и SHA-256. `UpdateInstaller` выполняет staging, version verification, backup, replace, post-install verification и rollback.

Release workflow после squash merge ветки `agent/sysdiff-vX.Y.Z` повторяет tests/package/smoke, создаёт tag, provenance attestations и GitHub Release.

## Безопасность

- existing database migration требует `--yes`;
- migration plan read-only;
- migration connections private и non-pooled;
- migration lock исключает параллельный apply;
- backup проверяется до SQL;
- unknown migration IDs и future user_version блокируют запись;
- providers выполняют только заранее определённое чтение;
- captured paths, commands, notes и tags остаются данными;
- plugin DLL загружается только через явный `--plugin`;
- `.sdshot` не доверяет ZIP names, declared size или checksum text;
- updater не заменяет работающий EXE напрямую;
- опасные действия требуют явного подтверждения.

## Тестирование

- Core tests: comparison, privacy, profiles, archive compatibility и migration integration;
- migration tests: backup preservation, bootstrap, idempotency, SQL rollback, future-version guard;
- Providers tests: severity/noise и Windows provider behavior;
- CLI tests: routers, TUI, updater, manifest и installer plan;
- integration tests: SQLite initialization и investigation persistence;
- smoke-test: version, migration status/history, compatibility matrix, doctor, timeline, cases, updater и TUI frame;
- release workflow: self-contained `win-x64`, package manifest, SHA-256 и release assets.
