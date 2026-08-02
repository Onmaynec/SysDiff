# 🗺️ Roadmap SysDiff

Roadmap отражает направление проекта, а не обещание конкретной даты.

## 0.1.0 — ядро MVP ✅

- [x] snapshots, SQLite и comparison;
- [x] базовые Windows providers;
- [x] Console/JSON/Markdown/HTML;
- [x] portable package, tests и GitHub Actions.

## 0.2.0 — расширение покрытия ✅

- [x] Firewall, приложения, драйверы и сертификаты;
- [x] ожидание дерева процессов;
- [x] дополнительные severity/noise rules.

## 0.3.0 — расследования ✅

- [x] live process/network monitor;
- [x] network configuration provider;
- [x] `.sdshot` и investigation bundle;
- [x] custom profiles;
- [x] cross-machine compare;
- [x] move/rename detection;
- [x] Provider SDK;
- [x] privacy redaction.

## 0.4.0 — Terminal Control Center ✅

- [x] полноэкранный dashboard;
- [x] стрелочная навигация;
- [x] Snapshot/Diff/Watch/Live modules;
- [x] reports, diagnostics и TUI tests.

## 0.5.0 — Cyber Console ✅

- [x] Cyber Control Node;
- [x] Command Deck `1–9`;
- [x] boot sequence;
- [x] Action Console;
- [x] Provider Stream;
- [x] neon theme;
- [x] safe motion/color modes.

## 0.6.0 — Drift Operations ✅

- [x] Baseline Vault;
- [x] Drift Scan;
- [x] explainable risk score;
- [x] Investigation Timeline;
- [x] Case Vault;
- [x] active case links;
- [x] additive SQLite migration;
- [x] legacy timeline reconstruction;
- [x] CLI/TUI/storage/risk tests.

## 0.7.0 — Release Channel ✅

- [x] tagged GitHub Releases;
- [x] release manifest и SHA-256;
- [x] provenance attestations;
- [x] stable updater;
- [x] staged install, backup и rollback;
- [x] Update Center.

## 0.8.0 — Compatibility Center ✅

- [x] format/schema compatibility matrix;
- [x] read-only `.sdshot` inspection;
- [x] JSON status для CI;
- [x] future schema rejection;
- [x] exact checksum validation;
- [x] manifest/snapshot invariant checks;
- [x] compatibility policy и recovery guide.

## 0.9.0 — Migration Lab ✅

- [x] database migration status и dry-run plan;
- [x] SQLite-consistent backup;
- [x] transaction rollback;
- [x] migration history и run audit;
- [x] exclusive migration lock;
- [x] `PRAGMA user_version` guard;
- [x] unknown/future database rejection;
- [x] additive metadata migration;
- [x] integration tests backup/idempotency/rollback;
- [x] recovery guide и JSON automation contract.

## Путь к 1.0

- [ ] JSON Schema для snapshot, comparison и bundle;
- [ ] golden fixtures версий 0.3–0.9;
- [ ] реальные handlers для breaking portable formats;
- [ ] полная reader/writer compatibility matrix;
- [ ] deprecation policy;
- [ ] tested upgrade chain до stable schema 1.0.

## 1.0.0 — стабильность

- [ ] стабильная публичная schema;
- [ ] полный migration path из поддерживаемых 0.x;
- [ ] Authenticode signed releases;
- [ ] безопасный rollback preview/handlers;
- [ ] snapshots с миллионами objects;
- [ ] полная RU/EN localization;
- [ ] гарантированная backward compatibility policy.

Актуальные задачи находятся в [GitHub Issues](https://github.com/Onmaynec/SysDiff/issues).
