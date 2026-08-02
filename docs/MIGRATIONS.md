# 🗃️ Migration Lab SysDiff

Migration Lab управляет внутренними версиями локальной SQLite-базы. Portable JSON/ZIP upgrades выполняются отдельным Legacy Bridge и не изменяют `sysdiff.db`.

## Быстрый старт

```powershell
sysdiff migration status
sysdiff migration plan
sysdiff migration plan --json
sysdiff migration history
sysdiff migration apply --yes
```

Без `--yes` применение отклоняется.

## Новая и существующая база

Если `sysdiff.db` отсутствовала, SysDiff создаёт таблицы и bootstrap текущего ledger без backup пустой базы.

Существующая база не мигрируется при обычном запуске. Пользователь сначала получает read-only plan и затем явно запускает apply.

## Что проверяет plan

- file existence;
- `PRAGMA quick_check`;
- `PRAGMA user_version`;
- known/unknown `app_migrations` IDs;
- pending steps;
- backup requirement;
- возможность safe apply.

| Status | Meaning |
|---|---|
| `Current` | все known migrations применены |
| `MigrationRequired` | существует последовательный safe path |
| `RequiresNewerSysDiff` | future user_version или unknown migration ID |
| `Invalid` | integrity/ledger inconsistency |

## Current database version

```text
PRAGMA user_version = 9
```

Это internal guard, а не public JSON schema major `1` и не product version `0.11.0`. База с более новым value отклоняется до store initialization.

## Backup и transaction

Перед изменением existing database:

```text
exclusive migration lock
      ↓
WAL checkpoint
      ↓
SQLite Backup API
      ↓
backup quick_check
      ↓
BEGIN transaction
      ↓
migration SQL + ledger records
      ↓
COMMIT
      ↓
post-commit quick_check
```

SQL failure откатывает schema и history record вместе. Failed post-commit integrity восстанавливает database из backup.

Default backup directory:

```text
<data-directory>\backups\migrations\
```

## Known migrations

### `0.6.0-investigations`

Добавляет investigation settings, cases, links и timeline events.

### `0.9.0-migration-lab`

Добавляет `migration_runs`, `database_metadata`, history indexes и устанавливает `user_version=9`.

Core snapshot/comparison tables не переписываются.

## Portable data — отдельный механизм

SysDiff 0.11.0 добавляет Legacy Bridge:

```powershell
sysdiff legacy plan comparison .\report-old.json
sysdiff legacy convert comparison .\report-old.json --yes
sysdiff legacy plan bundle .\investigation-old.zip
sysdiff legacy convert bundle .\investigation-old.zip --yes
```

Legacy Bridge использует обычный file backup и atomic output. Он не открывает SQLite и не записывает migration ledger. Migration Lab, Legacy Bridge, Schema Contract Center и Compatibility Center остаются независимыми safety layers.

Подробнее: [LEGACY_BRIDGE.md](LEGACY_BRIDGE.md).

## Восстановление SQLite вручную

1. закройте все процессы SysDiff;
2. сохраните текущий `sysdiff.db` отдельно;
3. выберите последний verified backup;
4. скопируйте backup на место database;
5. выполните `migration status`;
6. убедитесь в `Integrity: ok`;
7. повторно изучите `migration plan`.

Не заменяйте database, пока SysDiff или другой SQLite client держит её открытой.

## JSON automation

```powershell
sysdiff migration status --json
sysdiff migration plan --json
sysdiff migration history --json
sysdiff migration apply --yes --json
```

Enum statuses сериализуются строками, fields используют `camelCase`.

## Ограничения 0.11.0

- automatic migration existing database отсутствует;
- future/unknown migration definitions не угадываются;
- SQLite upgrade chain до будущего product 1.0 ещё не зафиксирован;
- portable conversion не импортирует данные автоматически;
- rollback handlers системных Windows changes не входят в Migration Lab;
- Authenticode пока не настроен.
