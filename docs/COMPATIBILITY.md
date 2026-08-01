# 🧩 Совместимость SysDiff

SysDiff 0.8.0 добавляет **Compatibility Center** для безопасной проверки переносимых снимков `.sdshot` до импорта.

> Compatibility Center не объявляет текущую внутреннюю schema стабильной публичной схемой 1.0. Он фиксирует текущее поведение reader и запрещает частичный импорт неизвестных форматов.

## Команды

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

`inspect` выполняет только чтение. Snapshot не сохраняется в SQLite, не становится baseline и не связывается с активным case.

## Матрица 0.8.0

| Объект | Текущая версия | Минимальная читаемая | Поведение |
|---|---:|---:|---|
| `.sdshot` container format | 1 | 1 | другие версии отклоняются |
| snapshot JSON schema | 1 | 1 | более новая требует обновления SysDiff |
| SQLite snapshot tables | legacy schema 1 | 1 | в 0.8.0 не изменяются |
| release manifest | 1 | 1 | проверяется updater 0.7+ |

## Статусы inspection

### `Compatible`

Manifest, checksum и snapshot согласованы. Архив может быть импортирован текущей версией.

### `RequiresNewerSysDiff`

`formatVersion` или `schemaVersion` выше поддерживаемой версии. Архив не импортируется, чтобы более старый reader не потерял неизвестные данные.

### `UnsupportedLegacy`

Версия ниже минимальной читаемой и для неё нет явно реализованного migration path. SysDiff не пытается угадывать преобразование.

### `Invalid`

Архив повреждён или нарушает инварианты: неправильный ZIP path, отсутствующая или дублирующаяся запись, неверный SHA-256, неизвестный format identifier, несовпадающий Snapshot ID или schema version.

## Что проверяет `.sdshot` inspection

1. файл существует и не превышает лимит archive size;
2. ZIP содержит разумное количество entries;
3. каждый entry находится только в корне и не содержит traversal/drive path;
4. `manifest.json`, `snapshot.json`, `checksums.sha256` присутствуют ровно один раз;
5. uncompressed snapshot не превышает установленный лимит;
6. checksum-файл содержит точные строки SHA-256 для manifest и snapshot;
7. JSON корректно десериализуется;
8. format identifier известен;
9. Snapshot ID совпадает;
10. schema version совпадает между manifest и snapshot;
11. версия входит в поддерживаемый диапазон.

## Машинный вывод

```json
{
  "archivePath": "C:\\cases\\before.sdshot",
  "status": "Compatible",
  "format": "SysDiff Snapshot",
  "formatVersion": 1,
  "schemaVersion": 1,
  "producerVersion": "0.8.0",
  "snapshotId": "00000000-0000-0000-0000-000000000000",
  "checksumsValid": true,
  "canImport": true,
  "message": "Архив совместим и может быть импортирован без миграции.",
  "warnings": []
}
```

Exit code `0` означает совместимость. Exit code `4` означает несовместимый или повреждённый переносимый формат.

## Политика развития схемы

- optional additive JSON fields могут добавляться только при сохранении корректного поведения старого reader;
- обязательное новое поле требует новой schema version;
- reader никогда не сохраняет частично прочитанный newer snapshot;
- migration должен быть отдельным, тестируемым и идемпотентным handler;
- преобразование обязано создавать backup или новый output, а не перезаписывать единственную копию;
- breaking change требует новой major version публичной схемы и upgrade guide;
- golden fixtures будущей стабильной схемы должны проверяться в CI.

## Recovery

Если архив получил `Invalid`:

1. не редактируйте исходный файл повторно;
2. сделайте копию для диагностики;
3. проверьте SHA-256 из источника архива;
4. повторно экспортируйте snapshot на исходной машине, если она доступна;
5. не извлекайте и не импортируйте отдельный `snapshot.json` вручную как доверенный снимок.

Если статус `RequiresNewerSysDiff`, обновите SysDiff через официальный stable channel и повторите inspection.
