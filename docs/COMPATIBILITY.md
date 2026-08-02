# 🧩 Совместимость SysDiff 0.10.0

SysDiff использует три независимых уровня защиты данных:

- **Schema Contract Center** проверяет JSON shape и semantics;
- **Compatibility Center** проверяет `.sdshot` container и reader range;
- **Migration Lab** проверяет и обновляет SQLite database.

## Команды

```powershell
sysdiff schema list --json
sysdiff schema validate snapshot .\snapshot.json --json
sysdiff compatibility inspect .\before.sdshot --json
sysdiff migration plan --json
```

## Reader/writer matrix

| Объект | Writer | Reader | Стабильность | Поведение |
|---|---:|---:|---|---|
| snapshot JSON contract | 1 | 1 | stable | additive fields accepted |
| comparison JSON contract | 1 | 1 | stable from 0.10 | future schema rejected |
| bundle manifest contract | 1 | 1 | stable from 0.10 | future schema rejected |
| `.sdshot` container | 1 | 1 | supported | ZIP/hash/invariants checked |
| SQLite `user_version` | 9 | `0..9` | internal | `>9` rejected before init |
| release manifest | 1 | 1 | stable updater contract | official host/hash required |

Public JSON schema major `1` не равен SQLite `user_version=9` и не означает product version 1.0.

## Producer compatibility

| Producer | `.sdshot` | Comparison report | Bundle manifest |
|---|---|---|---|
| 0.3–0.9 | readable legacy schema 1 | pre-contract | pre-contract |
| 0.10 | stable contract v1 | stable contract v1 | stable contract v1 |
| future, schema 1 + optional fields | readable | readable | readable |
| future, schema >1 | `RequiresNewerSysDiff` | `RequiresNewerSysDiff` | `RequiresNewerSysDiff` |

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
- minor: additive feature, optional property или новый independent contract;
- major: несовместимое product/API behavior.

### Schema SemVer

Public schema использует integer major:

- optional additive property остаётся schema `1`;
- required/type/casing/meaning change требует schema `2`;
- schema `2` обязана иметь отдельный `$id`, fixtures и migration guide.

## Deprecation

- stable field не удаляется без previous feature-release notice;
- deprecated optional field остаётся readable до следующей schema major;
- old reader не импортирует newer document частично;
- прекращение поддержки schema major требует migration guide или explicit `UnsupportedLegacy`.

## CI guarantees

CI проверяет:

- embedded schemas и Draft 2020-12 metadata;
- golden fixtures всех public contracts;
- unknown extension fields;
- missing required field;
- invalid enum;
- future schema rejection;
- package copies schemas/fixtures;
- release smoke через self-contained EXE.

## Recovery

### Invalid JSON contract

1. сохраните оригинал;
2. запустите validation с `--json`;
3. используйте `issues[].path` и `issues[].code`;
4. исправляйте только копию;
5. не понижайте schema version вручную.

### Invalid `.sdshot`

Проверьте SHA-256 источника и повторно экспортируйте snapshot. Не импортируйте извлечённый `snapshot.json` как доверенный объект в обход container checks.

### SQLite problem

Закройте SysDiff, сохраните database, восстановите verified backup из `backups\migrations`, затем выполните `migration status` и `migration plan`.

Подробнее: [SCHEMA_CONTRACT.md](SCHEMA_CONTRACT.md) и [MIGRATIONS.md](MIGRATIONS.md).
