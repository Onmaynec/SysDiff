# 🗺️ Roadmap SysDiff

Roadmap отражает направление проекта, а не обещание даты.

## Завершённые этапы

### 0.1.0–0.6.0 ✅

MVP, provider coverage, investigations, Terminal Control Center, Cyber Console и Drift Operations.

### 0.7.0 — Release Channel ✅

Tagged releases, manifest, SHA-256, provenance, stable updater, staged install и rollback.

### 0.8.0 — Compatibility Center ✅

Read-only `.sdshot` inspection, format/schema matrix, future rejection и exact checksum validation.

### 0.9.0 — Migration Lab ✅

Dry-run database plan, verified backup, transaction rollback, history, lock и future DB guard.

### 0.10.0 — Schema Contract ✅

Draft 2020-12 schemas, stable contract major 1, CLI validation, golden fixtures, reader/writer matrix и deprecation policy.

### 0.11.0 — Legacy Bridge ✅

Portable upgrade chain 0.3–0.9, backup, atomic output, checksums, fixtures и integration tests.

### 0.12.0 — Scale Lab ✅

- [x] SysDiff Artifact NDJSON v1;
- [x] synthetic generator до 10 000 000 artifacts;
- [x] external bounded-batch sort;
- [x] k-way merge chunk files;
- [x] streaming merge-join comparison;
- [x] streamed NDJSON changes;
- [x] managed/working-set/throughput telemetry;
- [x] benchmark regression exit code;
- [x] 1 000 000-artifact GitHub Actions gate;
- [x] benchmark artifact publication;
- [x] unit/integration tests и release smoke.

## Путь к product 1.0

### Data evolution

- [x] tested portable upgrade chain 0.3–0.9;
- [ ] migration handlers для будущего schema major 2;
- [ ] archival fixtures из каждого tagged historical release;
- [ ] rollback preview для системных изменений.

### Release trust

- [ ] Authenticode signed official executable;
- [ ] certificate rotation/revocation procedure;
- [ ] reproducibility verification beyond provenance attestations.

### Scale

- [x] bounded-memory file comparison на 1 000 000 artifacts;
- [x] external sort и streamed change report;
- [x] CI memory/throughput regression gate;
- [ ] streaming capture напрямую из Windows providers;
- [ ] paged SQLite readers вместо materialize `SnapshotRecord.Artifacts`;
- [ ] paginated HTML/Markdown generation;
- [ ] benchmark matrix на нескольких hardware tiers;

### UX и support

- [ ] полная RU/EN localization;
- [ ] accessibility audit Cyber Console;
- [ ] long-term support policy;

## 1.0.0 — стабильность

Product 1.0 требует одновременно:

- stable public Schema Contract v1 ✅;
- documented compatibility policy ✅;
- safe database migration foundation ✅;
- tested documented legacy upgrade paths ✅;
- bounded-memory million-object workflow ✅;
- Authenticode signed releases;
- provider-to-storage streaming path;
- complete localization/support documentation.

Актуальные задачи находятся в [GitHub Issues](https://github.com/Onmaynec/SysDiff/issues).
