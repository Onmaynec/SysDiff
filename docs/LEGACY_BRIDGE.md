# 🌉 Legacy Bridge

SysDiff 0.11.0 добавляет контролируемый upgrade path от переносимых форматов 0.3–0.9 к стабильному public Schema Contract v1.

Legacy Bridge не меняет исследуемую Windows-систему, локальную SQLite-базу или вложенные snapshots. Он работает только с явно указанным portable-файлом.

## Поддерживаемые источники

| Kind | Legacy source | Target |
|---|---|---|
| `comparison` | JSON report 0.3–0.9 без `format`, `formatVersion` и `sysDiffVersion` | Comparison Report schema v1 |
| `bundle` | Investigation ZIP 0.3–0.9 с manifest без `schemaVersion` и legacy `report.json` | Bundle manifest/report schema v1 |

`.sdshot` 0.3–0.9 уже использует snapshot schema `1`. Такие файлы проверяются Compatibility Center и не переписываются Legacy Bridge. Внутри bundle `before.sdshot` и `after.sdshot` копируются byte-for-byte.

## Команды

```powershell
sysdiff legacy matrix
sysdiff legacy matrix --json

sysdiff legacy plan comparison .\report-old.json
sysdiff legacy plan bundle .\investigation-old.zip

sysdiff legacy verify comparison .\report-v1.json
sysdiff legacy verify bundle .\investigation-v1.zip

sysdiff legacy convert comparison .\report-old.json --yes
sysdiff legacy convert bundle .\investigation-old.zip --yes
```

Короткие aliases верхнего уровня: `upgrade` и `bridge`.

## Output

Без `--output` создаётся side-by-side файл:

```text
report-old.schema-v1.json
investigation-old.schema-v1.zip
```

Явный путь:

```powershell
sysdiff legacy convert comparison .\old.json `
  --output .\converted\report.json --yes
```

Существующий output не заменяется без `--overwrite`. Для in-place conversion одновременно требуются:

```powershell
sysdiff legacy convert comparison .\old.json `
  --output .\old.json --overwrite --yes
```

Даже при side-by-side conversion создаётся backup исходника рядом с ним:

```text
old.legacy-backup-<UTC>-<GUID>.json
investigation.legacy-backup-<UTC>-<GUID>.zip
```

## Статусы

### `Current`

Файл уже соответствует Schema Contract v1. Повторный `convert` является безопасным no-op и не создаёт backup.

### `UpgradeAvailable`

Форма распознана как поддерживаемый legacy source и может быть преобразована без потери обязательных данных.

### `RequiresNewerSysDiff`

Документ или bundle использует schema version выше поддерживаемой. Legacy Bridge не выполняет downgrade и не угадывает неизвестную семантику.

### `UnsupportedLegacy`

Форма старая, но не соответствует документированному source shape 0.3–0.9. Автоматический repair не выполняется.

### `Invalid`

JSON, ZIP paths, required entries, SHA-256 или обязательные contract fields повреждены.

## Comparison migration

Старый report уже содержит `schemaVersion`, `generatedAtUtc`, `before`, `after` и `comparison`. Handler добавляет:

```json
{
  "format": "SysDiff Comparison Report",
  "formatVersion": 1,
  "schemaVersion": 1,
  "sysDiffVersion": "0.0.0-legacy"
}
```

`0.0.0-legacy` является честным sentinel: старый report не сохранял producer version, поэтому SysDiff не приписывает ему выдуманную версию.

Также добавляется additive provenance object `legacyMigration`. Исходные unknown extension fields сохраняются.

## Bundle migration

Перед планированием проверяются:

1. размер archive;
2. количество entries;
3. каждый ZIP path;
4. duplicate names;
5. обязательные `manifest.json`, `report.json`, `before.sdshot`, `after.sdshot`, `checksums.sha256`;
6. SHA-256 каждого payload entry;
7. JSON shape manifest/report;
8. public schema version.

При conversion:

- manifest получает `schemaVersion: 1` и provenance;
- legacy report получает comparison contract metadata;
- остальные entries сохраняются;
- `.sdshot` сохраняются byte-for-byte;
- `checksums.sha256` полностью пересчитывается;
- новый ZIP повторно открывается и проверяется.

## Safety sequence

```text
read-only plan
      ↓
source SHA-256
      ↓
automatic side-by-side backup
      ↓
transform in memory
      ↓
Schema Contract validation
      ↓
write temporary file
      ↓
atomic move
      ↓
full plan/ZIP/checksum validation of output
      ├─ Current → success + output SHA-256
      └─ failure → output removed or source restored from backup
```

## Exit codes

| Code | Meaning |
|---:|---|
| `0` | plan/current/converted successfully |
| `2` | invalid CLI arguments or missing `--yes` |
| `4` | invalid, unsupported, future or unconverted portable data |
| `5` | access denied |
| `8` | cancelled |

`legacy plan` returns `0` for both `Current` and `UpgradeAvailable`. `legacy verify` returns `0` only for `Current`.

## Recovery

Если conversion не завершился:

1. не удаляйте backup;
2. проверьте source SHA-256 в JSON result;
3. используйте новый side-by-side output только после `legacy verify`;
4. для in-place conversion при сомнении вручную восстановите backup;
5. сохраните повреждённый source отдельно для анализа.

Legacy Bridge не импортирует преобразованный файл автоматически. После conversion пользователь отдельно решает, импортировать snapshot/bundle или хранить его как evidence.
