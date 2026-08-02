# 🏗️ Архитектура SysDiff 0.11.0

## Цель

SysDiff разделяет capture Windows-данных, comparison, investigations, storage, reporting, portable upgrades, compatibility, migrations и public schema validation. TUI и CLI используют одинаковые services.

## Слои

```text
Cyber Control Node                         Non-interactive CLI
        │                                           │
        └────────────────────┬──────────────────────┘
                             ▼
      V11 → V10 → V9 → V8 → V7 → V6 → V4 → V3
                             │
       ┌─────────────────────┼──────────────────────────────────┐
       ▼                     ▼                                  ▼
Snapshot workflows      Drift Operations                   Data Safety
       │                     │                                  │
       ▼                     ├─ ComparisonEngine                ├─ PortableUpgradeService
SnapshotCoordinator         ├─ DriftRiskEngine                 ├─ SchemaContractService
       │                     ├─ Reporting                       ├─ SnapshotArchiveService
       ▼                     └─ Timeline / Cases                ├─ Compatibility Center
ISnapshotProvider[]                                             └─ DatabaseMigrationService
       └──────────────────────────────┬──────────────────────────────┘
                                      ▼
                     ISnapshotStore / IInvestigationStore
                                      │
                                      ▼
                                 SQLite sysdiff.db
```

## Проекты

| Проект | Ответственность |
|---|---|
| `SysDiff.Domain` | records, enums, общий product/schema version contract |
| `SysDiff.Core` | capture coordination, comparison, profiles, risk, privacy |
| `SysDiff.Storage` | SQLite, migrations, `.sdshot`, schema catalog, validation и portable upgrades |
| `SysDiff.Providers` | read-only Windows providers |
| `SysDiff.Reporting` | Console, JSON, Markdown и HTML writers |
| `SysDiff.ProviderSdk` | контракт внешних providers |
| `SysDiff.Cli` | DI, versioned routers, TUI, bundles, updater |

## Versioned routers

```text
V11CommandRouter
  ├── legacy matrix|status|plan|verify|convert
  └── V10CommandRouter
        ├── schema list|matrix|show|validate|verify
        └── V9CommandRouter
              ├── migration status|plan|history|apply
              └── V8CommandRouter
                    ├── compatibility status|matrix|inspect|verify
                    └── V7 → V6 → V4 → V3 → CommandApp
```

Каждый router перехватывает только команды своей версии и делегирует остальные вниз.

## PortableUpgradeService

Service находится в `SysDiff.Storage` и зависит только от `SchemaContractService`.

### Plan pipeline

```text
explicit kind + input path
       ↓
size/parser or ZIP guards
       ↓
source shape detection
       ├─ current v1     → Current
       ├─ supported 0.x  → UpgradeAvailable + ordered steps
       ├─ future schema  → RequiresNewerSysDiff
       ├─ unknown old    → UnsupportedLegacy
       └─ damaged        → Invalid
```

Plan не создаёт backup и не пишет output.

### Conversion pipeline

```text
read-only plan
      ↓
source SHA-256
      ↓
automatic side-by-side backup
      ↓
in-memory transform
      ↓
Schema Contract validation
      ↓
temporary file / ZIP
      ↓
atomic move
      ↓
full PlanAsync(output)
      ├─ Current → output SHA-256 + success
      └─ failure → output delete or in-place restore
```

### Comparison handler

Legacy report 0.3–0.9 содержит `schemaVersion`, `generatedAtUtc`, `before`, `after`, `comparison`, но не contract metadata. Handler добавляет:

```text
format = SysDiff Comparison Report
formatVersion = 1
schemaVersion = 1
sysDiffVersion = 0.0.0-legacy
legacyMigration = provenance object
```

Original nested JSON и unknown additive fields клонируются без semantic rewrite.

### Bundle handler

`ReadBundleAsync` проверяет:

- archive/entry size limits;
- count и duplicate entry names;
- path traversal/absolute paths;
- required entries;
- exact SHA-256 для каждого payload entry;
- manifest/report JSON shapes.

Conversion меняет только `manifest.json` и/или `report.json`. Остальные payload bytes сохраняются; `before.sdshot` и `after.sdshot` проверяются тестом byte-for-byte. Затем `checksums.sha256` строится заново.

### Idempotency

Current v1 file возвращает successful `Changed=false` до backup/output checks. Повторный convert является no-op.

## SchemaContractService

Catalog содержит три `SchemaContractDescriptor`:

```text
snapshot    → snapshot.schema.json
comparison  → comparison-report.schema.json
bundle      → investigation-bundle-manifest.schema.json
```

JSON Schema files включены в assembly как embedded resources. Validation проверяет required/type/UUID/RFC3339/SemVer/enum/range и возвращает JSON-path issues. `additionalProperties: true` сохраняет additive compatibility.

## Writer integration

### Snapshot

`SnapshotRecord` использует `SysDiffProduct.Version` и `PublicSchemaVersion`. `.sdshot` сохраняет исторический PascalCase contract.

### Comparison JSON

`JsonReportRenderer` пишет `format`, `formatVersion`, `schemaVersion`, `sysDiffVersion`, `generatedAtUtc` и camelCase domain objects.

### Bundle manifest

`InvestigationBundleService` self-validates manifest до checksums/ZIP creation.

## Golden и legacy fixtures

Schema fixtures проверяют current v1. Legacy fixture фиксирует реальную comparison shape 0.3–0.9.

Tests покрывают:

- valid current schemas;
- additive fields и future schema;
- legacy plan;
- backup equality;
- unknown-field preservation;
- repeated no-op;
- bundle snapshot byte equality;
- checksum rebuild;
- tampering rejection до backup.

Release smoke повторяет comparison plan/convert/verify через self-contained `sysdiff.exe`.

## Независимые линии versioning

```text
Public JSON schema major = 1
.sdshot container format = 1
SQLite PRAGMA user_version = 9
Product version = 0.11.0
```

Legacy Bridge преобразует portable JSON/ZIP. Compatibility Center проверяет `.sdshot` container. Migration Lab изменяет SQLite. Эти механизмы не вызывают друг друга автоматически.

## Release package

Portable ZIP включает:

```text
sysdiff.exe
LEGACY_BRIDGE.txt
legacy-fixtures/v0.9/*.json
SCHEMA_CONTRACT.txt
schemas/public/v1/*.schema.json
schema-fixtures/v1/*.json
MIGRATIONS.txt
COMPATIBILITY.txt
UPDATES.txt
```

Release workflow после squash merge повторяет tests, package, smoke, manifest/SHA-256 validation, provenance и asset verification.

## Безопасность

- plans read-only;
- portable conversion требует `--yes` и backup;
- future schema не downgrades;
- damaged/unknown legacy data не repair-ится автоматически;
- temporary output не считается успешным до post-validation;
- captured JSON values не выполняются как SQL или команды;
- migration требует backup и `--yes`;
- plugin DLL загружается только явно;
- updater не заменяет работающий EXE напрямую.
