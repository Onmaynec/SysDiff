# 📦 Переносимые форматы SysDiff 0.11.0

## `.sdshot`

`.sdshot` — ZIP-контейнер одного снимка:

```text
manifest.json
snapshot.json
checksums.sha256
```

### Экспорт и inspection

```powershell
sysdiff snapshot export before --output .\before.sdshot
sysdiff compatibility inspect .\before.sdshot
sysdiff compatibility inspect .\before.sdshot --json
```

Inspection read-only. Import использует ту же compatibility policy и сохраняет snapshot только при `canImport=true`.

### Проверки безопасности

- archive/JSON size limits;
- ограниченное число ZIP entries;
- каждый entry находится в корне;
- traversal, drive и absolute paths отклоняются;
- required entries присутствуют ровно один раз;
- exact SHA-256 lines;
- format, Snapshot ID и schema invariants;
- future schema отклоняется до сохранения.

Snapshot JSON schema `1` является stable public contract v1. Исторические `.sdshot` 0.3–0.9 с format/schema `1` остаются читаемыми и не требуют Legacy Bridge.

## Comparison JSON report

Writer 0.10+ создаёт:

```json
{
  "format": "SysDiff Comparison Report",
  "formatVersion": 1,
  "schemaVersion": 1,
  "sysDiffVersion": "0.11.0",
  "generatedAtUtc": "2026-08-02T00:00:00Z",
  "before": {},
  "after": {},
  "comparison": {}
}
```

Reports 0.3–0.9 содержат payload, но не `format`, `formatVersion` и `sysDiffVersion`. Их можно преобразовать:

```powershell
sysdiff legacy plan comparison .\report-old.json
sysdiff legacy convert comparison .\report-old.json --yes
sysdiff legacy verify comparison .\report-old.schema-v1.json
```

Поскольку producer version в старом report отсутствует, handler использует `0.0.0-legacy` и сохраняет provenance в additive `legacyMigration`.

## Investigation bundle

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

Writer 0.10+ self-validates manifest against public schema v1 до ZIP creation.

### Legacy bundle 0.3–0.9

```powershell
sysdiff legacy plan bundle .\investigation-old.zip
sysdiff legacy convert bundle .\investigation-old.zip --yes
sysdiff legacy verify bundle .\investigation-old.schema-v1.zip
```

Plan проверяет все ZIP paths, duplicate names, required entries, SHA-256 и JSON shape. Conversion:

- добавляет schema/provenance metadata в manifest;
- преобразует legacy report;
- сохраняет остальные entries;
- сохраняет `before.sdshot`/`after.sdshot` byte-for-byte;
- пересчитывает `checksums.sha256`;
- повторно открывает и валидирует output.

## Backup и output

Legacy Bridge по умолчанию пишет side-by-side:

```text
report-old.schema-v1.json
investigation-old.schema-v1.zip
```

До записи всегда создаётся source backup. Existing output требует `--overwrite`; conversion требует `--yes`.

## Public schemas

```text
schemas/public/v1/
├── snapshot.schema.json
├── comparison-report.schema.json
└── investigation-bundle-manifest.schema.json
```

Проверка отдельных JSON documents:

```powershell
sysdiff schema validate snapshot .\snapshot.json
sysdiff schema validate comparison .\report.json
sysdiff schema validate bundle .\manifest.json
```

## Независимые версии

| Значение | Current |
|---|---:|
| Product version | `0.11.0` |
| Public JSON schema | `1` |
| `.sdshot` container format | `1` |
| Investigation bundle format | `1` |
| SQLite `PRAGMA user_version` | `9` |

Подробнее: [LEGACY_BRIDGE.md](LEGACY_BRIDGE.md), [SCHEMA_CONTRACT.md](SCHEMA_CONTRACT.md) и [COMPATIBILITY.md](COMPATIBILITY.md).
