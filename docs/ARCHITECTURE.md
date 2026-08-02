# 🏗️ Архитектура SysDiff 0.10.0

## Цель

SysDiff разделяет capture Windows-данных, comparison, investigations, storage, reporting, compatibility, migrations и public schema validation. TUI и CLI используют одинаковые services.

## Слои

```text
Cyber Control Node                         Non-interactive CLI
        │                                           │
        └────────────────────┬──────────────────────┘
                             ▼
          V10 → V9 → V8 → V7 → V6 → V4 → V3
                             │
       ┌─────────────────────┼───────────────────────────────┐
       ▼                     ▼                               ▼
Snapshot workflows      Drift Operations                Data Contracts
       │                     │                               │
       ▼                     ├─ ComparisonEngine             ├─ SchemaContractService
SnapshotCoordinator         ├─ DriftRiskEngine              ├─ SnapshotArchiveService
       │                     ├─ Reporting                    ├─ Compatibility Center
       ▼                     └─ Timeline / Cases             └─ DatabaseMigrationService
ISnapshotProvider[]                  │
       └─────────────────────────────┴──────────────┐
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
| `SysDiff.Storage` | SQLite, migrations, `.sdshot`, schema catalog и validation |
| `SysDiff.Providers` | read-only Windows providers |
| `SysDiff.Reporting` | Console, JSON, Markdown и HTML writers |
| `SysDiff.ProviderSdk` | контракт внешних providers |
| `SysDiff.Cli` | DI, versioned routers, TUI, bundles, updater |

## Versioned routers

```text
V10CommandRouter
  ├── schema list|matrix|show|validate|verify
  └── V9CommandRouter
        ├── migration status|plan|history|apply
        └── V8CommandRouter
              ├── compatibility status|matrix|inspect|verify
              └── V7 → V6 → V4 → V3 → CommandApp
```

Каждый router перехватывает только команды своей версии и делегирует остальные вниз.

## SchemaContractService

Service находится в `SysDiff.Storage` и не зависит от CLI.

### Catalog

Catalog содержит три `SchemaContractDescriptor`:

```text
snapshot    → snapshot.schema.json
comparison  → comparison-report.schema.json
bundle      → investigation-bundle-manifest.schema.json
```

JSON Schema files включены в assembly как embedded resources. `schema show` читает именно embedded copy, а не отдельную ручную строку.

### Validation pipeline

```text
file path
   ↓
size + JSON parser guard
   ↓
contract kind
   ↓
schema version guard
   ├─ future → RequiresNewerSysDiff
   └─ supported
          ↓
required/type/format/enum/range validation
          ↓
Valid или Invalid + JSON-path issues
```

Validation read-only и не вызывает storage/import APIs.

### Compatibility principles

- `additionalProperties: true` во всех public schemas;
- unknown additive fields игнорируются validator и reader;
- missing required/type/enum errors отклоняются;
- future schema не интерпретируется как текущая;
- breaking change требует нового schema major.

## Writer integration

### Snapshot

`SnapshotRecord` использует `SysDiffProduct.Version` и `PublicSchemaVersion`. `.sdshot` сохраняет исторический PascalCase contract.

### Comparison JSON

`JsonReportRenderer` добавляет:

```text
format
formatVersion
schemaVersion
sysDiffVersion
generatedAtUtc
```

Nested domain objects сериализуются camelCase и строковыми enums.

### Bundle manifest

`InvestigationBundleService`:

1. формирует manifest с current producer version;
2. добавляет `schemaVersion=1`;
3. вызывает `SchemaContractService.ValidateJson`;
4. блокирует ZIP creation при invalid writer output;
5. только после этого создаёт checksums и archive.

## Golden fixtures

Fixtures копируются в test output. Tests проверяют:

- Draft 2020-12 и `$id` embedded resources;
- stable contract metadata;
- valid snapshot/comparison/bundle;
- additive extension fields;
- missing required field;
- invalid enum;
- future schema rejection.

Release smoke повторяет validation через self-contained `sysdiff.exe`.

## Snapshot Archive Compatibility

`SnapshotArchiveService.InspectAsync` независимо проверяет container integrity: ZIP paths, required entries, size, SHA-256, manifest, Snapshot ID и schema invariants. Это не заменяется JSON Schema validation.

```text
Schema Contract → shape и semantics JSON
Compatibility   → container/integrity/reader range
```

## Database Migration Service

Migration Lab остаётся отдельным механизмом SQLite:

```text
read-only plan
  → WAL checkpoint
  → verified backup
  → transaction
  → migration ledger
  → post-commit quick_check
```

`PRAGMA user_version=9` является внутренней database version и не связан с public JSON schema major 1.

## Release package

Portable ZIP включает:

```text
sysdiff.exe
SCHEMA_CONTRACT.txt
schemas/public/v1/*.schema.json
schema-fixtures/v1/*.json
MIGRATIONS.txt
COMPATIBILITY.txt
UPDATES.txt
```

Release workflow после squash merge повторяет tests, package, smoke, manifest/SHA-256 validation, provenance и asset verification.

## Безопасность

- schema validation не исправляет и не импортирует данные;
- captured JSON values не выполняются как SQL или команды;
- future schema отклоняется до partial interpretation;
- bundle writer self-validates;
- migration требует backup и `--yes`;
- `.sdshot` не доверяет ZIP names или checksum text;
- plugin DLL загружается только явно;
- updater не заменяет работающий EXE напрямую.
