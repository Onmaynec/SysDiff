# 📝 История изменений

Все заметные изменения SysDiff документируются здесь. Версии следуют Semantic Versioning.

## [Не выпущено]

### Планируется

- стабильная схема данных 1.0;
- полноценные migrations и compatibility policy;
- подписанные release artifacts;
- безопасный rollback preview;
- оптимизация больших снимков;
- полная локализация RU/EN.

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
