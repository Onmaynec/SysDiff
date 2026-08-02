# 📝 История изменений

Все заметные изменения SysDiff документируются здесь. Версии следуют Semantic Versioning.

## [Не выпущено]

### Планируется

- migration handlers для будущих schema major;
- Authenticode code signing;
- rollback preview системных изменений;
- оптимизация больших snapshots;
- полная RU/EN localization.

## [0.11.0] — 2026-08-02

### Добавлено

- Legacy Bridge с командами `legacy matrix|status|plan|verify|convert`;
- aliases `upgrade` и `bridge`;
- `PortableUpgradeService` и public plan/result models;
- handlers comparison reports и investigation bundles 0.3–0.9;
- статусы `Current`, `UpgradeAvailable`, `RequiresNewerSysDiff`, `UnsupportedLegacy`, `Invalid`;
- automatic source backup;
- source/output SHA-256 audit;
- atomic output и post-conversion verification;
- migration provenance в преобразованных JSON documents;
- documented sentinel `0.0.0-legacy` для неизвестной producer version;
- integration tests backup, no-op, future schema, checksum tampering и snapshot preservation;
- документация `docs/LEGACY_BRIDGE.md`;
- legacy fixture в portable package.

### Изменено

- версия продукта, EXE, package и smoke-test обновлена до `0.11.0`;
- current Schema Contract writer version синхронизируется с `SysDiffProduct.Version`;
- bundle conversion пересчитывает SHA-256 каждого payload entry;
- release smoke выполняет реальный flow `plan → convert → backup check → verify`;
- command chain расширена до `V11 → V10 → V9 → V8 → V7 → V6 → V4 → V3`.

### Совместимость

- public Schema Contract остаётся major `1`;
- `.sdshot` format/schema остаются `1` и не переписываются;
- SQLite `user_version` остаётся `9`;
- supported legacy source range: `0.3.0–0.9.x`;
- future schema не downgrades;
- unknown legacy shape не repair-ится автоматически;
- current v1 conversion является безопасным no-op.

### Безопасность

- plan read-only;
- conversion требует `--yes`;
- существующий output требует `--overwrite`;
- backup создаётся до первой записи;
- ZIP paths, duplicates, required entries и checksums проверяются до conversion;
- output пишется через temporary file и atomic move;
- failed post-validation удаляет side-by-side output или восстанавливает in-place source;
- преобразованный файл не импортируется автоматически.

## [0.10.0] — 2026-08-02

### Добавлено

- Schema Contract Center с командами `schema list|matrix|show|validate|verify`;
- стабильный public Schema Contract major `1`;
- Draft 2020-12 schemas для snapshot, comparison report и bundle manifest;
- embedded schema resources в `SysDiff.Storage`;
- `SchemaContractService` и public validation models;
- статусы `Valid`, `Invalid`, `RequiresNewerSysDiff`;
- required/type/UUID/RFC3339/SemVer/enum/range validation;
- golden fixtures для трёх contracts;
- tests missing required field, invalid enum, future schema и additive extensions;
- `V10CommandRouter` поверх chain 0.9;
- документация `docs/SCHEMA_CONTRACT.md`;
- reader/writer matrix и deprecation policy.

### Изменено

- версия EXE, snapshots, CyberTheme, package и smoke-test обновлена до `0.10.0`;
- версия продукта и schema contract централизованы в `SysDiffProduct`;
- JSON comparison report получил `format`, `formatVersion`, `schemaVersion`, `sysDiffVersion`;
- investigation bundle manifest получил текущую producer version и `schemaVersion=1`;
- bundle writer self-validates manifest до ZIP packaging;
- portable package включает schemas, golden fixtures и `SCHEMA_CONTRACT.txt`.

### Совместимость

- snapshot contract сохраняет исторический PascalCase `.sdshot`;
- comparison и bundle contracts используют camelCase;
- неизвестные additive properties разрешены;
- breaking required/type/casing/meaning change требует schema major `2`;
- future schema не интерпретируется частично;
- старые CLI-команды делегируются через `V10 → V9 → V8 → V7 → V6 → V4 → V3`.

### Безопасность

- schema validation read-only;
- invalid JSON получает точные path/code issues;
- future schema блокируется;
- writer output проверяется до bundle archive creation;
- JSON values не выполняются как код, SQL или команды;
- release smoke валидирует schemas и fixtures через published EXE.

## [0.9.0] — 2026-08-02

Migration Lab: dry-run database plan, verified SQLite backup, transaction rollback, migration history, lock, `PRAGMA user_version=9` и future database guard.

## [0.8.0] — 2026-08-01

Compatibility Center: read-only `.sdshot` inspection, schema/format matrix, exact SHA-256, ZIP guards и future schema rejection.

## [0.7.0] — 2026-08-01

Release Channel: tagged GitHub Releases, manifest, SHA-256, provenance attestations, updater, staged install, backup и rollback.

## [0.6.0] — 2026-08-01

Drift Operations: Baseline Vault, Drift Scan, risk score, Timeline, Case Vault и investigation storage.

## [0.5.0] — 2026-08-01

Cyber Console: Control Node, Command Deck, Provider Stream, boot sequence и safe motion/color modes.

## [0.4.0] — 2026-08-01

Terminal Control Center: fullscreen TUI, keyboard navigation и workflow modules.

## [0.3.0] — 2026-08-01

Investigations: live monitors, `.sdshot`, bundles, profiles, cross-machine compare, move detection, Provider SDK и privacy redaction.

## [0.2.0] — 2026-08-01

Расширенное provider coverage: Firewall, apps, drivers, certificates и severity/noise rules.

## [0.1.0] — 2026-08-01

MVP: Domain/Core/Storage/Providers/Reporting/CLI, snapshots, comparison, SQLite, tests и GitHub Actions.
