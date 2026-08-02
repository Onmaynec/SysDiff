# 🧩 Совместимость SysDiff 0.11.0

SysDiff использует четыре независимых уровня защиты данных:

- **Legacy Bridge** преобразует документированные portable shapes 0.3–0.9;
- **Schema Contract Center** проверяет JSON shape и semantics;
- **Compatibility Center** проверяет `.sdshot` container и reader range;
- **Migration Lab** проверяет и обновляет SQLite database.

## Команды

```powershell
sysdiff legacy matrix --json
sysdiff legacy plan comparison .\old-report.json --json
sysdiff schema list --json
sysdiff schema validate comparison .\report.json --json
sysdiff compatibility inspect .\before.sdshot --json
sysdiff migration plan --json
```

## Reader/writer matrix

| Объект | Writer | Reader | Legacy path | Поведение |
|---|---:|---:|---|---|
| snapshot JSON contract | 1 | 1 | не требуется | additive fields accepted |
| comparison JSON contract | 1 | 1 | pre-0.10 report → v1 | future schema rejected |
| bundle manifest/report | 1 | 1 | pre-0.10 ZIP → v1 | future schema rejected |
| `.sdshot` container | 1 | 1 | byte-preserved | ZIP/hash/invariants checked |
| SQLite `user_version` | 9 | `0..9` | Migration Lab | `>9` rejected before init |
| release manifest | 1 | 1 | не требуется | official host/hash required |

Public JSON schema major `1` не равен SQLite `user_version=9` и не означает product version 1.0.

## Producer compatibility

| Producer | `.sdshot` | Comparison report | Investigation bundle |
|---|---|---|---|
| 0.3–0.9 | readable schema 1 | `UpgradeAvailable` | `UpgradeAvailable` |
| 0.10–0.11 | current contract v1 | current contract v1 | current contract v1 |
| future, schema 1 + optional fields | readable | readable | readable |
| future, schema >1 | `RequiresNewerSysDiff` | `RequiresNewerSysDiff` | `RequiresNewerSysDiff` |

## Legacy Bridge statuses

- `Current` — portable data уже соответствует contract v1;
- `UpgradeAvailable` — распознан безопасный handler 0.3–0.9;
- `RequiresNewerSysDiff` — source использует future schema;
- `UnsupportedLegacy` — old shape не имеет документированного handler;
- `Invalid` — JSON/ZIP/checksum/required fields повреждены.

Plan read-only. Convert требует `--yes`, создаёт backup, пишет атомарно и повторно проверяет output. Current conversion является no-op.

### Что преобразуется

Comparison report получает отсутствующие contract metadata и provenance. Старый report не содержит producer version, поэтому используется `0.0.0-legacy`.

Bundle получает schema metadata в manifest/report и новый `checksums.sha256`. Вложенные `.sdshot` сохраняются byte-for-byte.

### Что не преобразуется

- future schema;
- произвольный повреждённый JSON;
- unknown ZIP layout;
- `.sdshot`, уже читаемый Compatibility Center;
- SQLite database.

## Schema Contract statuses

- `Valid` — required/type/format/enum checks пройдены;
- `Invalid` — contract нарушен;
- `RequiresNewerSysDiff` — schema version выше reader.

Unknown additive properties разрешены. Они не должны превращать valid schema 1 document в invalid.

## `.sdshot` statuses

- `Compatible` — archive integrity и reader range valid;
- `RequiresNewerSysDiff` — format/schema newer;
- `UnsupportedLegacy` — отсутствует migration path;
- `Invalid` — ZIP, JSON, checksum или invariants повреждены.

Inspection read-only и не сохраняет snapshot.

## SQLite statuses

- `Current` — integrity и ledger актуальны;
- `MigrationRequired` — известен safe path;
- `RequiresNewerSysDiff` — future user_version или unknown migration ID;
- `Invalid` — integrity/ledger inconsistent.

Existing database migration требует `migration apply --yes`, verified backup и transaction.

## SemVer и schema versioning

### Product SemVer

- patch: bug fix без изменения public contract;
- minor: additive feature, optional property или новый migration handler;
- major: несовместимое product/API behavior.

### Schema major

- optional additive property остаётся schema `1`;
- required/type/casing/meaning change требует schema `2`;
- schema `2` обязана иметь отдельный `$id`, fixtures и migration guide.

## Deprecation

- stable field не удаляется без previous feature-release notice;
- deprecated optional field остаётся readable до следующей schema major;
- old reader не импортирует newer document частично;
- прекращение поддержки source shape требует migration guide или explicit `UnsupportedLegacy`.

## CI guarantees

CI проверяет:

- embedded schemas и golden fixtures;
- legacy comparison fixture;
- unknown extension preservation;
- backup byte equality;
- current-file idempotency;
- future schema rejection;
- bundle checksum rebuild;
- nested snapshot byte equality;
- tampered checksum rejection до backup;
- release smoke `plan → convert → verify` через self-contained EXE.

## Recovery

### Failed legacy conversion

1. сохраните source и automatic backup;
2. используйте только output, прошедший `legacy verify`;
3. сравните source/output SHA-256 из JSON result;
4. не понижайте schema version вручную;
5. при in-place сомнении восстановите backup.

### Invalid `.sdshot`

Проверьте SHA-256 источника и повторно экспортируйте snapshot. Не импортируйте извлечённый `snapshot.json` в обход container checks.

### SQLite problem

Закройте SysDiff, сохраните database, восстановите verified backup из `backups\migrations`, затем выполните `migration status` и `migration plan`.

Подробнее: [LEGACY_BRIDGE.md](LEGACY_BRIDGE.md), [SCHEMA_CONTRACT.md](SCHEMA_CONTRACT.md) и [MIGRATIONS.md](MIGRATIONS.md).
