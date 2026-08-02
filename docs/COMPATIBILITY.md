# 🧩 Совместимость SysDiff 0.12.0

SysDiff использует пять независимых data mechanisms:

- **Scale Lab** обрабатывает operational NDJSON streams;
- **Legacy Bridge** преобразует documented portable shapes 0.3–0.9;
- **Schema Contract Center** проверяет public JSON shape;
- **Compatibility Center** проверяет `.sdshot` container;
- **Migration Lab** обновляет SQLite database.

## Matrix

| Объект | Current version | Reader | Upgrade/normalization | Поведение |
|---|---:|---:|---|---|
| Scale Artifact NDJSON | 1 | 1 | `scale sort` | sorted unique identity required |
| Scale change NDJSON | 1 | 1 | не требуется | streamed operational output |
| snapshot JSON contract | 1 | 1 | не требуется | additive fields accepted |
| comparison JSON contract | 1 | 1 | pre-0.10 → Legacy Bridge | future schema rejected |
| bundle manifest/report | 1 | 1 | pre-0.10 → Legacy Bridge | future schema rejected |
| `.sdshot` container | 1 | 1 | byte-preserved | ZIP/hash/invariants checked |
| SQLite `user_version` | 9 | `0..9` | Migration Lab | `>9` rejected before init |
| release manifest | 1 | 1 | не требуется | official host/hash required |

Scale format `1`, public schema `1`, `.sdshot` format `1`, SQLite `9` и product `0.12.0` являются разными version lines.

## Scale stream rules

`scale compare` принимает NDJSON, отсортированный по `identity` с `OrdinalIgnoreCase` ordering.

Отклоняются:

- invalid JSON;
- missing/empty identity;
- duplicate identity;
- descending identity order;
- line больше 4 MiB.

`scale sort` выполняет bounded-memory normalization через chunks и atomic output. Scale NDJSON не импортируется в SQLite автоматически и не считается public portable evidence format.

## Portable producer compatibility

| Producer | `.sdshot` | Comparison report | Investigation bundle |
|---|---|---|---|
| 0.3–0.9 | readable schema 1 | `UpgradeAvailable` | `UpgradeAvailable` |
| 0.10–0.12 | current contract v1 | current contract v1 | current contract v1 |
| future, schema 1 + optional fields | readable | readable | readable |
| future, schema >1 | `RequiresNewerSysDiff` | `RequiresNewerSysDiff` | `RequiresNewerSysDiff` |

## Legacy Bridge statuses

- `Current`;
- `UpgradeAvailable`;
- `RequiresNewerSysDiff`;
- `UnsupportedLegacy`;
- `Invalid`.

Plan read-only. Convert требует `--yes`, backup, atomic output и post-validation. Current conversion — no-op.

## Schema Contract statuses

- `Valid`;
- `Invalid`;
- `RequiresNewerSysDiff`.

Unknown additive properties разрешены. Breaking required/type/casing/meaning change требует schema major `2`.

## `.sdshot` и SQLite

Compatibility inspection не сохраняет snapshot. SQLite migration требует `migration apply --yes`, verified backup и transaction.

## CI guarantees

CI проверяет:

- embedded schemas и golden fixtures;
- legacy fixture и conversion safety;
- Scale unit tests;
- unsorted/duplicate rejection;
- release smoke через published EXE;
- Scale benchmark на 1 000 000 artifacts;
- managed-memory, throughput и expected-change gates;
- benchmark artifact publication.

## Recovery

### Scale failure

1. не изменяйте source NDJSON;
2. при unsorted error выполните `scale sort` в новый файл;
3. исправьте duplicate identity в producer;
4. используйте `scale-benchmark.json` для memory/throughput diagnosis;
5. не объединяйте partial temporary outputs.

### Portable failure

Сохраните source/backup, используйте только output после `legacy verify`, не понижайте schema вручную.

### SQLite failure

Закройте процессы SysDiff, восстановите verified migration backup, затем выполните `migration status` и `migration plan`.

Подробнее: [SCALE_LAB.md](SCALE_LAB.md), [LEGACY_BRIDGE.md](LEGACY_BRIDGE.md), [SCHEMA_CONTRACT.md](SCHEMA_CONTRACT.md) и [MIGRATIONS.md](MIGRATIONS.md).
