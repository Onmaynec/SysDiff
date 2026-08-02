# 🏗️ Архитектура SysDiff 0.12.0

## Общая схема

```text
Cyber Control Node                         Non-interactive CLI
        │                                           │
        └────────────────────┬──────────────────────┘
                             ▼
 V12 → V11 → V10 → V9 → V8 → V7 → V6 → V4 → V3
                             │
       ┌─────────────────────┼────────────────────────────────────┐
       ▼                     ▼                                    ▼
Snapshot workflows      Drift Operations                     Data Systems
       │                     │                                    │
SnapshotCoordinator         ├─ ComparisonEngine                  ├─ ScaleLabService
       │                     ├─ DriftRiskEngine                   ├─ PortableUpgradeService
ISnapshotProvider[]         └─ Timeline / Cases                  ├─ SchemaContractService
                                                                    ├─ SnapshotArchiveService
                                                                    ├─ Compatibility Center
                                                                    └─ DatabaseMigrationService
                                      │
                                      ▼
                     ISnapshotStore / IInvestigationStore
                                      │
                                      ▼
                                 SQLite sysdiff.db
```

## Versioned routers

```text
V12CommandRouter
  ├── scale matrix|synth|sort|compare|benchmark
  └── V11CommandRouter
        ├── legacy matrix|status|plan|verify|convert
        └── V10CommandRouter
              ├── schema list|matrix|show|validate|verify
              └── V9CommandRouter
                    ├── migration status|plan|history|apply
                    └── V8 → V7 → V6 → V4 → V3 → CommandApp
```

Каждый router перехватывает только собственный command family.

## ScaleLabService

Service находится в `SysDiff.Core` и использует `SystemArtifact` как line payload.

### Operational stream

```text
SysDiff Artifact NDJSON v1
one SystemArtifact JSON object per line
identity ordering: OrdinalIgnoreCase ascending
```

Это отдельный operational format. Он не меняет public snapshot/comparison/bundle Schema Contract v1.

### Synthetic writer

```text
for index 0..N
  create one artifact
  serialize one line
  flush through buffered writer
```

В памяти не создаётся коллекция из N artifacts.

### External sort

```text
input NDJSON
    ↓
read bounded batch (default 50 000)
    ↓
sort by identity + duplicate check
    ↓
temporary chunk files
    ↓
PriorityQueue k-way merge
    ↓
atomic sorted output
```

State состоит из одного batch и одного current line на chunk. Temporary directory удаляется в `finally`.

### Streaming compare

```text
sorted before cursor ─┐
                      ├─ identity merge join ─ change NDJSON writer
sorted after cursor ──┘
```

Для одинаковой identity сравниваются provider/type/display/tags/properties. Result содержит только counters и telemetry, а не `List<SystemChange>`.

### Memory telemetry

Периодически измеряются:

- `GC.GetTotalMemory(false)` — managed gate;
- process working set — диагностическая метрика;
- processed/written/bytes read;
- artifacts per second.

`scale benchmark` сохраняет `scale-benchmark.json` и возвращает `10`, если memory, throughput или expected-change gate не выполнен.

## Обычный ComparisonEngine

Существующий engine сохраняет severity, noise filtering и move/rename heuristic. Он строит dictionaries из `SnapshotRecord.Artifacts` и предназначен для обычных snapshot sizes.

```text
обычный workflow → compare
million-file workflow → scale compare
```

## Data safety services

### PortableUpgradeService

Преобразует documented portable formats 0.3–0.9 в Schema Contract v1 с backup, SHA-256 audit, atomic output и post-validation.

### SchemaContractService

Проверяет snapshot/comparison/bundle JSON shape: required/type/UUID/RFC3339/SemVer/enum/range. Unknown additive properties разрешены.

### Compatibility Center

Проверяет `.sdshot` ZIP paths, entries, IDs, format/schema и checksums без import.

### DatabaseMigrationService

```text
read-only plan → WAL checkpoint → verified backup → transaction → ledger → quick_check
```

## Независимые версии

```text
Product version              0.12.0
Public JSON schema major     1
Scale NDJSON format          1
.sdshot container format     1
SQLite PRAGMA user_version   9
```

## CI topology

```text
build.yml   → compile, self-contained publish, release smoke
test.yml    → full xUnit suite, JSON validation
scale.yml   → 1 000 000 artifacts, memory/throughput/count gate, artifact upload
```

`scale-benchmark-1000000` содержит benchmark JSON, console JSON и streamed changes.

## Release package

```text
sysdiff.exe
SCALE_LAB.txt
LEGACY_BRIDGE.txt
SCHEMA_CONTRACT.txt
MIGRATIONS.txt
COMPATIBILITY.txt
UPDATES.txt
schemas/public/v1/*.schema.json
schema-fixtures/v1/*.json
legacy-fixtures/v0.9/*.json
```

## Security invariants

- scale inputs никогда не выполняются;
- line size и synthetic count ограничены;
- invalid JSON, missing identity, duplicates и unsorted compare input отклоняются;
- outputs публикуются через temporary file и atomic move;
- future portable schema не downgrades;
- portable/database mutations требуют backup и explicit confirmation;
- plugin DLL загружается только явно;
- updater не заменяет running EXE напрямую.
