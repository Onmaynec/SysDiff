# 📐 SysDiff Schema Contract v1

SysDiff 0.10.0 опубликовал первый стабильный public contract переносимых JSON-данных. SysDiff 0.11.0 добавляет Legacy Bridge, который преобразует документированные pre-contract reports/bundles 0.3–0.9 в этот contract без изменения schema major.

## Контракты

| Key | Документ | Schema | Minimum reader | Current writer |
|---|---|---:|---:|---:|
| `snapshot` | `snapshot.json` внутри `.sdshot` | 1 | 0.10.0 | 0.11.0 |
| `comparison` | JSON comparison report | 1 | 0.10.0 | 0.11.0 |
| `bundle` | `manifest.json` investigation bundle | 1 | 0.10.0 | 0.11.0 |

Стандарт: **JSON Schema Draft 2020-12**.

```text
schemas/public/v1/
├── snapshot.schema.json
├── comparison-report.schema.json
└── investigation-bundle-manifest.schema.json
```

Golden fixtures:

```text
tests/fixtures/schema/v1/
├── snapshot.valid.json
├── comparison-report.valid.json
└── investigation-bundle-manifest.valid.json
```

Legacy fixture:

```text
tests/fixtures/legacy/v0.9/comparison-report.legacy.json
```

## CLI validation

```powershell
sysdiff schema list
sysdiff schema list --json
sysdiff schema show snapshot
sysdiff schema show comparison
sysdiff schema show bundle
sysdiff schema validate snapshot .\snapshot.json
sysdiff schema validate comparison .\report.json --json
sysdiff schema validate bundle .\manifest.json --json
```

Aliases: `schemas`, `contract`, `schema verify`. Exit code `0` означает valid contract, `4` — invalid/future document.

## Legacy upgrade

Pre-contract comparison reports 0.3–0.9 уже содержат core payload, но не имеют `format`, `formatVersion` и `sysDiffVersion`. Investigation bundles того же периода имеют manifest без `schemaVersion` и legacy report.

```powershell
sysdiff legacy plan comparison .\report-old.json
sysdiff legacy convert comparison .\report-old.json --yes
sysdiff legacy verify comparison .\report-old.schema-v1.json

sysdiff legacy plan bundle .\investigation-old.zip
sysdiff legacy convert bundle .\investigation-old.zip --yes
```

Legacy Bridge создаёт backup, пишет output атомарно и принимает результат только после Schema Contract/ZIP/checksum validation. `.sdshot` 0.3–0.9 не переписываются.

Старый comparison report не сохранял producer version, поэтому handler использует честный sentinel `0.0.0-legacy` и additive provenance object.

## Статусы validation

### `Valid`

Required fields, types, formats, enum values и supported schema version корректны.

### `Invalid`

Document нарушает contract: missing field, неверный UUID/date/SemVer/type/enum или damaged JSON.

### `RequiresNewerSysDiff`

Document schema version выше reader. SysDiff не интерпретирует unknown major как старую форму.

## Правила совместимости

### Additive change

Разрешено внутри schema major `1`:

- новое optional field;
- additive extension object;
- новая optional metadata;
- дополнительный array item, соответствующий существующему item contract.

Все public schemas используют:

```json
{
  "additionalProperties": true
}
```

Reader игнорирует неизвестные properties. Golden и legacy fixtures намеренно содержат extension fields, чтобы CI проверял сохранение этого поведения.

### Breaking change

Требует schema major `2`:

- removal/rename required field;
- type/casing/meaning change;
- удаление enum value;
- optional → required;
- incompatible container layout.

Breaking change обязан иметь новый `$id`, schema directory, migration guide, fixtures, old-reader rejection test и new-reader migration/unsupported test.

## Deprecation policy

- stable contract v1 не удаляется молча;
- deprecation публикуется минимум в одном предыдущем feature release;
- deprecated optional field остаётся readable до следующей schema major;
- reader не импортирует newer document частично;
- прекращение поддержки schema/source shape требует migration guide или explicit `UnsupportedLegacy`.

## Casing

`snapshot.json` исторически PascalCase. Contract v1 сохраняет это для `.sdshot` 0.3–0.9. Comparison report и bundle manifest используют camelCase. Смена casing является breaking change.

## Writer invariants

### Snapshot

Identity, creation metadata, producer/schema version, profile/status, architecture, provider results и artifacts обязательны.

### Comparison report

Writer 0.10+ создаёт:

```json
{
  "format": "SysDiff Comparison Report",
  "formatVersion": 1,
  "schemaVersion": 1,
  "sysDiffVersion": "0.11.0"
}
```

### Investigation bundle

Manifest содержит current producer version и `schemaVersion: 1`. Writer self-validates до ZIP packaging.

## Validation scope

`SchemaContractService` проверяет:

- required properties;
- JSON types;
- UUID и RFC3339;
- SemVer, включая prerelease+build;
- enum values и numeric ranges;
- supported schema version;
- nested provider/artifact/change/privacy structures.

JSON Schema files являются source of truth для external validators; embedded copies используются `schema show`. Validation read-only и не импортирует/исправляет документ.

## Reader/writer matrix

| Producer | Snapshot v1 | Comparison | Bundle | Поведение reader 0.11 |
|---|---|---|---|---|
| 0.3–0.9 | current-compatible | `UpgradeAvailable` | `UpgradeAvailable` | `.sdshot` читается; report/bundle конвертируются явно |
| 0.10–0.11 | stable v1 | stable v1 | stable v1 | full validation |
| future writer, schema 1 | additive v1 | additive v1 | additive v1 | unknown fields accepted |
| future writer, schema >1 | newer | newer | newer | `RequiresNewerSysDiff` |

## CI guarantees

CI проверяет:

- Draft 2020-12 metadata и `$id` embedded schemas;
- current golden fixtures;
- additive fields;
- missing required/invalid enum/future schema;
- legacy plan и conversion;
- backup byte equality;
- unknown-field preservation;
- idempotent current no-op;
- bundle checksum rebuild и nested snapshot byte equality;
- tampered checksum rejection;
- release smoke через published EXE.

## Recovery

При invalid current document сохраните original, используйте `schema validate --json` и исправляйте только копию.

При legacy source используйте `legacy plan`, затем `legacy convert --yes`; доверяйте только output, прошедшему `legacy verify`. Не понижайте schema number вручную и не удаляйте automatic backup до завершения проверки.
