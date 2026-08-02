# 📐 SysDiff Schema Contract v1

SysDiff 0.10.0 публикует первый стабильный публичный контракт переносимых JSON-данных. Контракт отделён от версии приложения: продукт остаётся в серии `0.x`, но public schema major `1` уже имеет формальные правила совместимости.

## Контракты

| Key | Документ | Schema | Writer 0.10 | Reader 0.10 |
|---|---|---:|---:|---:|
| `snapshot` | `snapshot.json` внутри `.sdshot` | 1 | 1 | 1 |
| `comparison` | JSON comparison report | 1 | 1 | 1 |
| `bundle` | `manifest.json` investigation bundle | 1 | 1 | 1 |

Стандарт: **JSON Schema Draft 2020-12**.

Файлы:

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

Portable release включает копии schemas и fixtures для offline validation.

## CLI

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

Aliases:

```powershell
sysdiff schemas matrix
sysdiff contract verify snapshot .\snapshot.json
```

Exit code `0` означает valid contract. Exit code `4` означает invalid document или schema version, требующую более новую версию SysDiff.

## Статусы validation

### `Valid`

Обязательные поля присутствуют, типы и форматы корректны, enum-значения известны, schema version поддерживается.

### `Invalid`

Документ нарушает contract: отсутствует обязательное поле, неверен UUID/date/SemVer, тип или enum, либо JSON повреждён.

### `RequiresNewerSysDiff`

Document schema version выше текущего reader. SysDiff не пытается интерпретировать неизвестную major/minor форму как старую.

## Правила совместимости

### Additive change

Разрешено без новой schema major:

- новое optional поле;
- новый объект extension в неизвестном property;
- новая необязательная metadata;
- дополнительный элемент массива, соответствующий существующему item contract.

Все public schemas используют:

```json
{
  "additionalProperties": true
}
```

Reader обязан игнорировать неизвестные свойства, а не считать документ повреждённым. Golden fixtures намеренно содержат extension fields, чтобы CI проверял это поведение.

### Breaking change

Требует schema major `2`:

- удаление или переименование обязательного поля;
- изменение типа обязательного поля;
- изменение смысла существующего поля;
- удаление enum value;
- превращение optional field в required;
- смена casing существующего property;
- несовместимое изменение container layout.

Breaking change обязан иметь:

1. новый `$id` и отдельный schema directory;
2. migration/upgrade guide;
3. новый golden fixture;
4. test старого reader rejection;
5. test нового reader migration или explicit unsupported status;
6. release note с SemVer impact.

## Deprecation policy

- stable contract v1 не удаляется молча;
- deprecation публикуется минимум в одном предыдущем feature release;
- deprecated field остаётся readable до следующей schema major;
- writer может прекратить создавать deprecated optional field только после documented notice;
- reader не должен частично импортировать document из более новой schema;
- поддержка schema major прекращается только с migration guide или явным `UnsupportedLegacy`.

## Casing

`snapshot.json` исторически сериализуется с PascalCase. Contract v1 сохраняет это, чтобы не ломать `.sdshot` 0.3–0.9.

Comparison report и bundle manifest используют camelCase. Смена casing является breaking change.

## Writer invariants

### Snapshot

Обязательны identity, creation metadata, producer version, schema version, profile/status, architecture, provider results и artifacts.

### Comparison report

0.10 добавляет явные поля:

```json
{
  "format": "SysDiff Comparison Report",
  "formatVersion": 1,
  "schemaVersion": 1,
  "sysDiffVersion": "0.10.0"
}
```

### Investigation bundle

Manifest теперь содержит текущую producer version вместо legacy hard-coded `0.3.0`, а также `schemaVersion: 1`. Writer валидирует manifest через `SchemaContractService` до ZIP packaging.

## Validation scope

`SchemaContractService` выполняет contract-specific validation:

- required properties;
- object/array/string/number/boolean types;
- UUID;
- RFC 3339 timestamps;
- stable SemVer;
- enum values;
- numeric ranges;
- supported schema version;
- nested snapshot/provider/artifact/change/privacy structures.

Стандартные JSON Schema files остаются source of truth для внешних validators. Embedded copies используются CLI-командой `schema show`.

Validation является read-only. Она не импортирует snapshot, не меняет SQLite, не создаёт case/baseline и не исправляет документ автоматически.

## Reader/writer matrix

| Producer | Snapshot v1 | Comparison v1 | Bundle v1 | Поведение 0.10 reader |
|---|---:|---:|---:|---|
| 0.3–0.9 | legacy v1 shape | pre-contract | pre-contract | `.sdshot` читается; reports/bundles не объявляются contract-valid |
| 0.10 | stable v1 | stable v1 | stable v1 | full validation |
| future writer, schema 1 | stable v1 + additive fields | stable v1 + additive fields | stable v1 + additive fields | unknown fields accepted |
| future writer, schema >1 | newer | newer | newer | `RequiresNewerSysDiff` |

## CI guarantees

`SchemaContractServiceTests` проверяют:

- все embedded schemas являются Draft 2020-12;
- `$id`, stable status и contract version совпадают с catalog;
- три golden fixtures валидны;
- неизвестные fields разрешены;
- missing required field отклоняется с JSON path;
- invalid enum отклоняется;
- future schema version получает `RequiresNewerSysDiff`.

Release smoke дополнительно проверяет CLI catalog, embedded schema output и все golden fixtures через опубликованный `sysdiff.exe`.

## Recovery

При `Invalid`:

1. сохраните исходный JSON без изменений;
2. выполните validation с `--json`;
3. исправляйте копию, ориентируясь на `issues[].path` и `issues[].code`;
4. не импортируйте отдельные фрагменты как доверенные данные;
5. повторно создайте report/bundle исходной версией SysDiff, если она доступна.

При `RequiresNewerSysDiff` обновите SysDiff через stable release channel и повторите validation. Downgrade schema number вручную запрещён: это скрывает неизвестные поля, но не делает их совместимыми.
