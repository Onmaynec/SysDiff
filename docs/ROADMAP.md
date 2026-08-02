# 🗺️ Roadmap SysDiff

Roadmap отражает направление проекта, а не обещание даты.

## Завершённые этапы

### 0.1.0 — ядро MVP ✅

Snapshots, SQLite, comparison, базовые providers, reports, tests и CI.

### 0.2.0 — расширение покрытия ✅

Firewall, apps, drivers, certificates, process-tree waiting и severity/noise rules.

### 0.3.0 — расследования ✅

Live monitor, `.sdshot`, bundles, custom profiles, cross-machine compare, move detection, Provider SDK и privacy redaction.

### 0.4.0 — Terminal Control Center ✅

Fullscreen dashboard, keyboard navigation, Snapshot/Diff/Watch/Live modules и TUI tests.

### 0.5.0 — Cyber Console ✅

Control Node, Command Deck, boot sequence, Action Console, Provider Stream и safe animation/color modes.

### 0.6.0 — Drift Operations ✅

Baseline Vault, Drift Scan, risk score, Timeline, Case Vault и additive investigation storage.

### 0.7.0 — Release Channel ✅

Tagged releases, manifest, SHA-256, provenance, stable updater, staged install и rollback.

### 0.8.0 — Compatibility Center ✅

Read-only `.sdshot` inspection, format/schema matrix, future rejection и exact checksum validation.

### 0.9.0 — Migration Lab ✅

Dry-run database plan, verified backup, transaction rollback, history, lock и future DB guard.

### 0.10.0 — Schema Contract ✅

- [x] Draft 2020-12 schemas для snapshot, comparison и bundle;
- [x] stable public contract major 1;
- [x] embedded schema catalog;
- [x] CLI validation и machine-readable issues;
- [x] golden fixtures;
- [x] additive extension policy;
- [x] future schema rejection;
- [x] reader/writer matrix;
- [x] deprecation и breaking-change policy;
- [x] self-validating bundle writer;
- [x] offline schemas/fixtures в portable package.

## Путь к product 1.0

### Data evolution

- [ ] реальные migration handlers для будущего schema major 2;
- [ ] tested upgrade chain для всех поддерживаемых legacy formats;
- [ ] compatibility fixtures реальных exports 0.3–0.9;
- [ ] rollback preview для системных изменений.

### Release trust

- [ ] Authenticode signed official executable;
- [ ] documented certificate rotation/revocation procedure;
- [ ] reproducibility verification beyond current provenance attestations.

### Scale и UX

- [ ] snapshots с миллионами objects;
- [ ] streaming report generation;
- [ ] полная RU/EN localization;
- [ ] accessibility audit Cyber Console;
- [ ] long-term support policy.

## 1.0.0 — стабильность

Product 1.0 требует одновременно:

- stable public Schema Contract v1 ✅;
- documented compatibility policy ✅;
- safe database migration foundation ✅;
- tested legacy upgrade paths;
- Authenticode signed releases;
- performance targets для large snapshots;
- complete localization/support documentation.

Актуальные задачи находятся в [GitHub Issues](https://github.com/Onmaynec/SysDiff/issues).
