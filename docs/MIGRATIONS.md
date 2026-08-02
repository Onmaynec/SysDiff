# 🗃️ Migration Lab SysDiff

SysDiff 0.9.0 добавляет контролируемые миграции локальной SQLite-базы. Механизм предназначен для безопасного перехода между внутренними версиями хранения до фиксации публичной schema 1.0.

## Быстрый старт

Проверить состояние без изменений:

```powershell
sysdiff migration status
sysdiff migration plan
sysdiff migration plan --json
```

Посмотреть историю:

```powershell
sysdiff migration history
sysdiff migration history --json
```

Применить ожидающие миграции:

```powershell
sysdiff migration apply --yes
sysdiff migration apply --yes --json
```

Без `--yes` применение отклоняется.

## Политика запуска

### Новая база

Если `sysdiff.db` не существовала до запуска, SysDiff создаёт обычные таблицы и сразу выполняет bootstrap текущего migration ledger. Backup пустой новой базы не создаётся.

### Существующая база

Обычный запуск не применяет новые миграции автоматически. Пользователь сначала получает план и только затем явно запускает `migration apply --yes`.

Это разделяет запуск приложения и изменение существующих пользовательских данных.

## Что проверяет план

Migration Lab проверяет:

- наличие файла базы;
- `PRAGMA quick_check`;
- текущий `PRAGMA user_version`;
- известные записи `app_migrations`;
- неизвестные migration IDs;
- список ожидающих шагов;
- необходимость резервной копии;
- возможность безопасного применения.

Статусы:

| Статус | Значение |
|---|---|
| `Current` | все известные миграции применены |
| `MigrationRequired` | есть ожидающие безопасные шаги |
| `RequiresNewerSysDiff` | база создана более новой или неизвестной версией |
| `Invalid` | нарушена целостность или ledger не согласован с `user_version` |

## `PRAGMA user_version`

Версия 0.9.0 использует:

```text
PRAGMA user_version = 9
```

Это внутренний целочисленный guard, а не публичная версия JSON/SQLite schema. База с `user_version` выше поддерживаемого отклоняется до вызова store initialization, чтобы старая сборка не пыталась модифицировать более новую структуру.

## Резервная копия

Перед изменением существующей базы SysDiff:

1. получает exclusive migration lock;
2. выполняет WAL checkpoint;
3. создаёт согласованную копию через SQLite backup API;
4. запускает `quick_check` копии;
5. только после этого начинает SQL transaction.

Путь по умолчанию:

```text
<data-directory>\backups\migrations\
    sysdiff-YYYYMMDD-HHMMSSfff-before-<migration-id>.db
```

В portable mode `<data-directory>` находится рядом с `sysdiff.exe` в каталоге `data`.

## Транзакции и rollback

Каждая migration definition выполняется в отдельной SQLite transaction.

```text
plan
  ↓
backup
  ↓
begin transaction
  ↓
migration SQL
  ↓
app_migrations + migration_runs
  ↓
commit
  ↓
quick_check
```

Если SQL завершается ошибкой, transaction откатывается. Записи migration history не остаются применёнными частично.

Если итоговый `quick_check` после commit не проходит, SysDiff восстанавливает базу из созданного backup.

## Migration ledger 0.9

Миграция `0.9.0-migration-lab` добавляет:

```text
migration_runs
    id
    migration_id
    started_utc
    finished_utc
    status
    backup_path
    error

database_metadata
    key
    value
    updated_utc
```

Также создаются индексы истории и устанавливается `PRAGMA user_version = 9`.

Существующие таблицы snapshots, artifacts, comparisons, investigations, cases и timeline не переписываются.

## Восстановление вручную

Если требуется ручное восстановление:

1. полностью закройте все процессы SysDiff;
2. сохраните повреждённый `sysdiff.db` отдельно для диагностики;
3. выберите последний проверенный backup;
4. скопируйте backup на место `sysdiff.db`;
5. запустите `sysdiff migration status`;
6. убедитесь, что `Integrity: ok`;
7. повторно выполните `migration plan`.

Не заменяйте базу, пока SysDiff или другой SQLite-клиент держит её открытой.

## JSON для автоматизации

```powershell
sysdiff migration status --json
sysdiff migration plan --json
sysdiff migration history --json
sysdiff migration apply --yes --json
```

Enum-статусы сериализуются строками, имена полей используют `camelCase`.

## Ограничения 0.9.0

- стабильная публичная schema 1.0 ещё не объявлена;
- миграция 0.9 additive и не конвертирует переносимые `.sdshot`;
- automatic migration существующей базы отсутствует;
- rollback handlers системных изменений не входят в Migration Lab;
- Authenticode code signing пока не настроен.
