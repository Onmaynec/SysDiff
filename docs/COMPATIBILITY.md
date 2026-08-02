# 🧩 Совместимость SysDiff

SysDiff 0.9.0 объединяет две независимые линии защиты данных:

- **Compatibility Center** проверяет переносимые `.sdshot` до импорта;
- **Migration Lab** проверяет и обновляет локальную SQLite-базу.

> Текущие внутренние schema versions ещё не объявлены stable public schema 1.0. SysDiff фиксирует reader behavior, блокирует unknown/newer data и требует явного migration path.

## Переносимые снимки

```powershell
sysdiff compatibility status
sysdiff compatibility status --json
sysdiff compatibility inspect .\before.sdshot
sysdiff compatibility inspect .\before.sdshot --json
```

Короткий alias:

```powershell
sysdiff compat matrix
sysdiff compat verify .\before.sdshot
```

`inspect` выполняет только чтение. Snapshot не сохраняется в SQLite, не становится baseline и не связывается с active case.

## Локальная база

```powershell
sysdiff migration status
sysdiff migration plan
sysdiff migration history
sysdiff migration apply --yes
```

`migration status` и `plan` read-only. Existing database изменяется только после `migration apply --yes` и создания backup.

## Матрица 0.9.0

| Объект | Текущая версия | Минимальная читаемая | Поведение |
|---|---:|---:|---|
| `.sdshot` container format | 1 | 1 | другие версии отклоняются |
| snapshot JSON schema | 1 | 1 | более новая требует обновления SysDiff |
| SQLite `PRAGMA user_version` | 9 | 0 | `>9` отклоняется до initialization |
| migration ledger | `0.9.0-migration-lab` | `0.6.0-investigations` | unknown IDs блокируют apply |
| release manifest | 1 | 1 | проверяется updater 0.7+ |

SQLite `user_version=9` является внутренним guard и не означает stable schema 9 или schema 1.0.

## Статусы `.sdshot` inspection

### `Compatible`

Manifest, checksum и snapshot согласованы. Архив может быть импортирован текущей версией.

### `RequiresNewerSysDiff`

`formatVersion` или `schemaVersion` выше поддерживаемой версии. Архив не импортируется, чтобы старый reader не потерял неизвестные данные.

### `UnsupportedLegacy`

Версия ниже минимальной читаемой и для неё нет явно реализованного migration path. SysDiff не пытается угадывать преобразование.

### `Invalid`

Архив повреждён или нарушает инварианты: неправильный ZIP path, отсутствующая или дублирующаяся запись, неверный SHA-256, неизвестный format identifier, несовпадающий Snapshot ID или schema version.

## Статусы SQLite

### `Current`

`quick_check` успешен, `user_version` поддерживается, все известные migrations применены.

### `MigrationRequired`

Существует последовательный известный путь. Пользователь может изучить dry-run plan и явно применить его.

### `RequiresNewerSysDiff`

`user_version` выше поддерживаемого или ledger содержит неизвестный migration ID. Текущая версия не изменяет базу.

### `Invalid`

Нарушена целостность либо ledger и `user_version` противоречат друг другу.

## Что проверяет `.sdshot` inspection

1. file/archive size limits;
2. количество ZIP entries;
3. каждый entry path;
4. единственные обязательные entries;
5. uncompressed size;
6. точные SHA-256 строки;
7. JSON parsing;
8. format identifier;
9. Snapshot ID;
10. schema version между manifest и payload;
11. поддерживаемый reader range.

## Что проверяет migration plan

1. `PRAGMA quick_check`;
2. `PRAGMA user_version`;
3. существование `app_migrations`;
4. известные и неизвестные IDs;
5. pending definitions;
6. destructive и backup flags;
7. возможность безопасного apply.

## Машинный вывод `.sdshot`

```json
{
  "archivePath": "C:\\cases\\before.sdshot",
  "status": "Compatible",
  "format": "SysDiff Snapshot",
  "formatVersion": 1,
  "schemaVersion": 1,
  "producerVersion": "0.9.0",
  "checksumsValid": true,
  "canImport": true,
  "warnings": []
}
```

## Политика развития схемы

- optional additive JSON fields допускаются только при сохранении поведения старого reader;
- обязательное новое поле требует новой schema version;
- reader никогда не сохраняет частично прочитанный newer snapshot;
- database migration должна быть последовательной, идемпотентной и транзакционной;
- existing database migration обязана создавать проверенный backup;
- unknown migration ID и future user_version не игнорируются;
- breaking change требует новой major version публичной схемы и upgrade guide;
- golden fixtures stable schema должны проверяться в CI.

## Recovery

Для повреждённого `.sdshot` сохраните исходник, проверьте SHA-256 и повторно экспортируйте snapshot на исходной машине.

Для проблем SQLite:

1. полностью закройте SysDiff;
2. сохраните текущую базу отдельно;
3. восстановите последний backup из `backups\migrations`;
4. запустите `sysdiff migration status`;
5. убедитесь в `Integrity: ok`;
6. повторно изучите `migration plan`.

Подробнее: [MIGRATIONS.md](MIGRATIONS.md).
