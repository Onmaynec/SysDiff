# 📦 Переносимые форматы SysDiff 0.8.0

## `.sdshot`

`.sdshot` — ZIP-контейнер одного снимка:

```text
manifest.json
snapshot.json
checksums.sha256
```

### Экспорт

```powershell
sysdiff snapshot export before --output .\before.sdshot
```

### Проверка до импорта

```powershell
sysdiff compatibility inspect .\before.sdshot
sysdiff compatibility inspect .\before.sdshot --json
```

Inspection является read-only: snapshot не сохраняется в SQLite и не меняет baseline/active case.

### Импорт

```powershell
sysdiff snapshot import .\before.sdshot
```

`ImportAsync` использует ту же compatibility policy, что и inspection. Сохранение начинается только при `canImport=true`.

### Проверки безопасности

- максимальный размер входного архива — 512 МБ;
- максимальный размер JSON снимка — 1 ГБ;
- число ZIP entries ограничено;
- каждый entry должен находиться в корне архива;
- path traversal, drive paths и абсолютные пути отклоняются;
- `manifest.json`, `snapshot.json`, `checksums.sha256` должны присутствовать ровно один раз;
- SHA-256 проверяется по отдельным точным строкам;
- JSON обязан корректно десериализоваться;
- format identifier должен быть `SysDiff Snapshot`;
- Snapshot ID и schema version должны совпадать между manifest и snapshot;
- более новая schema отклоняется до сохранения;
- данные архива никогда не выполняются.

## Manifest

Основные поля:

```json
{
  "format": "SysDiff Snapshot",
  "formatVersion": 1,
  "schemaVersion": 1,
  "sysDiffVersion": "0.8.0",
  "snapshotId": "00000000-0000-0000-0000-000000000000",
  "createdAtUtc": "2026-08-01T00:00:00Z"
}
```

JSON serializer может использовать PascalCase в фактическом archive payload; reader является case-insensitive. Семантика полей и version policy остаются инвариантными.

## 🧳 Investigation bundle

```powershell
sysdiff bundle create <comparison-id> --output .\investigation.zip
```

Состав:

```text
manifest.json
checksums.sha256
before.sdshot
after.sdshot
report.html
report.json
report.md
```

Bundle не включает сырые логи, дампы памяти или приватные ключи. Перед передачей архива третьей стороне всё равно проверьте имена приложений, сертификатов и системные метаданные.

## Совместимость

Версия 0.8.0 использует `formatVersion: 1` и `schemaVersion: 1`. Снимки 0.3–0.7 с этими значениями остаются читаемыми.

Статусы Compatibility Center:

- `Compatible` — можно импортировать;
- `RequiresNewerSysDiff` — reader старее формата;
- `UnsupportedLegacy` — отсутствует безопасный migration handler;
- `Invalid` — нарушена структура, integrity или manifest invariant.

Текущая schema `1` ещё не объявлена стабильной публичной schema 1.0. Подробнее: [COMPATIBILITY.md](COMPATIBILITY.md).
