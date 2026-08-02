# 📝 История изменений

Все заметные изменения SysDiff документируются здесь. Версии следуют Semantic Versioning.

## [Не выпущено]

### Планируется

- migration handlers для будущих schema major;
- Authenticode code signing;
- rollback preview системных изменений;
- streaming capture из Windows providers напрямую в storage;
- полная RU/EN localization.

## [0.12.0] — 2026-08-02

### Добавлено

- Scale Lab с командами `scale matrix|synth|sort|compare|benchmark`;
- aliases `large` и `stream`;
- operational format `SysDiff Artifact NDJSON v1`;
- synthetic generator до 10 000 000 artifacts;
- external chunk sort и k-way merge;
- streaming merge-join comparison;
- NDJSON change writer без materialize списка изменений;
- managed memory, working set и throughput telemetry;
- machine-readable benchmark result и exit code `10` для regression;
- xUnit tests streaming comparison, sort, unsorted rejection и benchmark result;
- dedicated GitHub Actions benchmark на 1 000 000 artifacts;
- CI artifact `scale-benchmark-1000000`;
- документация `docs/SCALE_LAB.md` и portable `SCALE_LAB.txt`.

### Изменено

- версия продукта, EXE, package и smoke-test обновлена до `0.12.0`;
- command chain расширена до `V12 → V11 → V10 → V9 → V8 → V7 → V6 → V4 → V3`;
- release smoke создаёт два datasets, сравнивает их и запускает benchmark gate;
- public Schema Contract остаётся v1 и не включает operational NDJSON stream.

### Производительность

- generator держит один artifact;
- sorter держит один bounded batch и cursor на chunk;
- comparison держит только текущую before/after пару;
- change report пишется по одной строке;
- CI gate: 1 000 000 artifacts, managed heap ≤256 MiB, throughput ≥1 000 artifacts/sec;
- duplicate identity и unsorted input обнаруживаются до недостоверного результата.

### Безопасность

- максимальная NDJSON line ограничена 4 MiB;
- synthetic count ограничен 10 000 000;
- invalid JSON и missing identity отклоняются;
- input files не изменяются;
- sort/compare output публикуется через temporary file и atomic move;
- benchmark regression блокирует workflow отдельным exit code.

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

Schema Contract Center: stable public Schema Contract v1, Draft 2020-12 schemas, embedded catalog, golden fixtures, CLI validation и compatibility policy.

## [0.9.0] — 2026-08-02

Migration Lab: dry-run database plan, verified backup, transaction rollback, migration history, lock, `PRAGMA user_version=9` и future database guard.

## [0.8.0] — 2026-08-01

Compatibility Center: read-only `.sdshot` inspection, schema/format matrix, exact SHA-256, ZIP guards и future schema rejection.

## [0.7.0] — 2026-08-01

Release Channel: tagged GitHub Releases, manifest, SHA-256, provenance attestations, updater, staged install, backup и rollback.

## [0.6.0] — 2026-08-01

Drift Operations: Baseline Vault, Drift Scan, risk score, Timeline, Case Vault и investigation storage.

## [0.5.0] — 2026-08-01

Cyber Console: Control Node, Command Deck, Provider Stream, boot sequence и safe motion/color modes.

## [0.4.0] — 2026-08-01

Terminal Control Center: fullscreen dashboard, keyboard navigation и workflow modules.

## [0.3.0] — 2026-08-01

Investigations: live monitors, `.sdshot`, bundles, profiles, cross-machine compare, move detection, Provider SDK и privacy redaction.

## [0.2.0] — 2026-08-01

Расширенное provider coverage: Firewall, apps, drivers, certificates и severity/noise rules.

## [0.1.0] — 2026-08-01

MVP: Domain/Core/Storage/Providers/Reporting/CLI, snapshots, comparison, SQLite, tests и GitHub Actions.
