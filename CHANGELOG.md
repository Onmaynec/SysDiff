# 📝 История изменений

Все заметные изменения SysDiff документируются здесь. Версии следуют Semantic Versioning.

## [Не выпущено]

### Планируется

- стабильная публичная схема данных 1.0;
- migration handlers для будущих breaking schema changes;
- Authenticode code signing официального EXE;
- безопасный rollback preview системных изменений;
- оптимизация больших снимков;
- полная локализация RU/EN.

## [0.9.0] — 2026-08-02

### Добавлено

- Migration Lab с командами `migration status|plan|history|apply`;
- отдельный `V9CommandRouter` поверх маршрутизации 0.8;
- публичные модели migration plan, result, history и run records;
- additive migration `0.9.0-migration-lab`;
- таблицы `migration_runs` и `database_metadata`;
- `PRAGMA user_version = 9` как guard внутренней версии SQLite;
- SQLite-consistent backup через backup API;
- exclusive migration lock;
- JSON-вывод со строковыми enum statuses;
- отдельная документация `docs/MIGRATIONS.md`;
- integration tests backup, idempotency, rollback и future-version rejection.

### Изменено

- версия EXE, snapshot metadata, CyberTheme, package и smoke-test обновлена до `0.9.0`;
- новая пустая база автоматически получает текущий migration ledger без backup;
- существующая база не мигрируется при обычном запуске;
- startup отклоняет базу с `user_version` выше поддерживаемого до store initialization;
- portable package включает `MIGRATIONS.txt`;
- migration connections изолированы private non-pooled SQLite cache.

### Совместимость

- snapshot/comparison/investigation tables не переписываются;
- `.sdshot` format/schema остаются `1`;
- stable public schema 1.0 пока не объявляется;
- migration history 0.6 признаётся известной;
- неизвестные migration IDs блокируют изменение базы;
- старые CLI-команды делегируются через `V9 → V8 → V7 → V6 → V4 → V3`.

### Безопасность

- `migration plan` является read-only dry-run;
- `migration apply` требует явного `--yes`;
- backup создаётся и проверяется до первого migration SQL;
- WAL checkpoint выполняется перед backup;
- SQL и migration history записываются в одной transaction;
- ошибка SQL откатывает transaction без partial history;
- post-commit `quick_check` контролирует целостность;
- при неуспешной итоговой проверке существующая база восстанавливается из backup;
- параллельные migrations блокируются lock-файлом.

## [0.8.0] — 2026-08-01

### Добавлено

- Compatibility Center с командами `compatibility status|matrix|inspect|verify`;
- машинный JSON-вывод матрицы поддерживаемых format/schema versions;
- безопасная inspection `.sdshot` без записи в SQLite;
- публичная модель `SnapshotArchiveInspection`;
- статусы `Compatible`, `RequiresNewerSysDiff`, `UnsupportedLegacy` и `Invalid`;
- отдельный `V8CommandRouter` поверх маршрутизации 0.7;
- документация `docs/COMPATIBILITY.md`;
- тесты совместимого архива и корректно подписанной будущей схемы.

### Изменено

- версия EXE, snapshot metadata, CyberTheme, package и smoke-test обновлена до `0.8.0`;
- `SnapshotArchiveService.ImportAsync` использует единую compatibility policy;
- packaging включает offline-руководство `COMPATIBILITY.txt`;
- SemVer validation в `package.ps1` теперь корректно якорится целиком.

### Совместимость

- `.sdshot` format version `1` и snapshot schema version `1` остаются текущими;
- inspection не объявляет schema `1.0` стабильной публичной схемой продукта;
- архив из более новой версии отклоняется до сохранения snapshot;
- архив без безопасного migration path получает явный `UnsupportedLegacy`;
- старые команды делегируются через `V8 → V7 → V6 → V4 → V3`.

### Безопасность

- проверяются все ZIP entry paths, а не только обязательные файлы;
- дубликаты `manifest.json`, `snapshot.json` и `checksums.sha256` отклоняются;
- SHA-256 сверяется по отдельным точным строкам checksum-файла;
- manifest и snapshot обязаны иметь одинаковые Snapshot ID и schema version;
- неизвестная или будущая схема не импортируется частично;
- inspection не изменяет SQLite и пользовательские данные.

## [0.7.0] — 2026-08-01

### Добавлено

- полноценный release pipeline, который ждёт squash merge release PR;
- аннотированный tag `v0.7.0` на merge-коммите;
- GitHub Release с portable ZIP, `.sha256` и `release-manifest.json`;
- GitHub artifact provenance attestations для ZIP и manifest;
- строгая модель stable release manifest;
- SemVer parser/comparer без зависимости от NuGet updater package;
- команды `update check|status|download|install|settings|clear-cache`;
- JSON-вывод update status/settings;
- автоматическая проверка stable channel;
- опциональная автоматическая загрузка без автоматической установки;
- Update Center внутри `System Node`;
- безопасная ZIP extraction с path/size guards;
- staged `sysdiff.exe --version` verification;
- PowerShell update helper с ожиданием PID, backup, post-install verification и rollback;
- локальные `update-settings.json`, `update-state.json` и update cache;
- JSON Schema для release manifest;
- тесты SemVer, manifest tampering, SHA-256, persistence и installer plan;
- отдельная документация `docs/UPDATES.md`;
- SVG-preview Release Channel.

### Изменено

- версия EXE, snapshot metadata, CyberTheme и portable package обновлена до `0.7.0`;
- `scripts/package.ps1` теперь проверяет версию проекта и формирует manifest;
- `scripts/smoke-test.ps1` проверяет updater CLI и Release Channel smoke frame;
- System Node получил Update Center;
- README и русская документация синхронизированы с tagged releases;
- release workflow стал idempotent и не создаёт duplicate release.

### Безопасность

- updater принимает только stable manifest официального репозитория;
- asset URL ограничен HTTPS-host/path allow-list;
- size и SHA-256 проверяются до распаковки;
- ZIP traversal и чрезмерная распаковка отклоняются;
- работающий EXE не заменяется напрямую;
- helper получает пути через `ArgumentList` и использует `-LiteralPath`;
- failed verification восстанавливает backup;
- self-update недоступен при `dotnet run`;
- установка всегда требует явного подтверждения;
- пользовательские SQLite, snapshots, cases, reports и profiles не удаляются;
- отсутствие Authenticode не скрывается: manifest содержит `unsigned=true`.

### Release engineering

- tag/version/branch/package/manifest должны совпадать;
- test suite и smoke-test повторяются после merge перед созданием tag;
- release assets проверяются после публикации;
- существующий GitHub Release приводит к безопасному no-op.

## [0.6.0] — 2026-08-01

### Добавлено

- Baseline Vault с командами `baseline show|set|clear`;
- Drift Scan относительно доверенного snapshot;
- explainable Drift Risk Score `0–100`;
- уровни `Stable`, `Notice`, `Elevated`, `High`, `Critical`;
- Investigation Timeline со snapshots, comparisons, scans, cases и notes;
- Case Vault с active case, tags, description и links;
- команды `case create|list|show|use|close`;
- команды `timeline list` и `drift scan`;
- автоматическая привязка snapshot/comparison/report к активному кейсу;
- additive SQLite tables `investigation_settings`, `investigation_cases`, `investigation_links`, `timeline_events`;
- таблица `app_migrations` и запись `0.6.0-investigations`;
- новые TUI-модули `[03] Drift Operations`, `[04] Investigation Timeline`, `[05] Case Vault`;
- `System Node`, объединяющий diagnostics, settings, about и disconnect;
- новые hotkeys `G`, `T`, `K`, `D`;
- JSON summary и HTML report каждого Drift Scan;
- integration tests persistence/migration и risk score tests;
- SVG-preview Drift Operations.

### Изменено

- Cyber Control Node показывает baseline, active case и последний risk score;
- Command Deck снова содержит ровно девять основных модулей;
- версия EXE, snapshot metadata и portable package обновлена до `0.6.0`;
- `--help` и `--tui-smoke` расширены Drift Operations;
- русская документация полностью синхронизирована.

### Совместимость

- legacy tables snapshots/artifacts/comparisons не меняются;
- initialization новых tables idempotent;
- старые snapshots/comparisons реконструируются в Timeline без rewrite;
- stable schema 1.0 не объявляется преждевременно.

### Безопасность

- baseline не выполняет rollback;
- Drift Scan не изменяет исследуемую систему;
- notes, tags, paths и captured commands считаются данными;
- закрытие кейса не удаляет snapshots или reports;
- partial data явно снижает доверие к risk summary.

## [0.5.0] — 2026-08-01

### Добавлено

- `SYSDIFF CYBER CONSOLE` с ASCII-header и плотным Control Node;
- нумерованный Command Deck `[01]`…`[09]`;
- быстрые клавиши `1–9`, `P/B/A`, `C`, `W`, `L`, `D`;
- boot sequence;
- Action Console;
- animated progress/scanner bars;
- живой Provider Stream;
- единая neon theme;
- `SYSDIFF_NO_ANIMATIONS=1` и `NO_COLOR=1`;
- Cyber Console tests и SVG-preview.

## [0.4.0] — 2026-08-01

### Добавлено

- полноэкранный Terminal Control Center;
- управление стрелками;
- Snapshot Center, Comparison Lab, Watch Session и Live Monitor;
- Reports & Bundles, Diagnostics, Settings и About;
- TUI tests и smoke frame.

## [0.3.0] — 2026-08-01

### Добавлено

- live process/network monitor;
- network configuration provider;
- `.sdshot` и investigation bundle;
- пользовательские профили;
- cross-machine compare;
- move/rename detection;
- Provider SDK;
- privacy redaction.

## [0.2.0] — 2026-08-01

### Добавлено

- Windows Firewall, установленные приложения, драйверы и сертификаты;
- ожидание дерева процессов;
- дополнительные severity/noise rules.

## [0.1.0] — 2026-08-01

### Добавлено

- архитектура Domain/Core/Storage/Providers/Reporting/CLI;
- snapshots, comparison, reports и SQLite;
- portable package, tests и GitHub Actions.
