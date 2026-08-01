# 🏗️ Архитектура SysDiff 0.8.0

## Цель

SysDiff разделяет сбор Windows-данных, comparison, investigations, хранение, отчёты, terminal UI, release channel и compatibility policy. Интерактивный Cyber Console и non-interactive CLI используют одинаковые application services.

## Слои

```text
Cyber Control Node                         Non-interactive CLI
        │                                           │
        └────────────────────┬──────────────────────┘
                             ▼
                  V8 → V7 → V6 → V4 → V3
                             │
       ┌─────────────────────┼──────────────────────┐
       ▼                     ▼                      ▼
Snapshot workflows      Drift Operations      Compatibility Center
       │                     │                      │
       ▼                     ├─ ComparisonEngine    └─ SnapshotArchiveService
SnapshotCoordinator         ├─ DriftRiskEngine            │
       │                     ├─ Reporting                  ├─ ZIP guards
       ▼                     └─ Timeline / Cases           ├─ SHA-256
ISnapshotProvider[]                  │                     ├─ manifest invariants
       │                             │                     └─ format/schema policy
       └──────────────┬──────────────┘
                      ▼
             ISnapshotStore / IInvestigationStore
                      │
                      ▼
                 SQLite sysdiff.db
```

## Проекты solution

| Проект | Ответственность |
|---|---|
| `SysDiff.Domain` | records, enums и storage contracts |
| `SysDiff.Core` | capture coordination, comparison, profiles, risk и privacy |
| `SysDiff.Storage` | SQLite, `.sdshot`, compatibility inspection |
| `SysDiff.Providers` | read-only Windows data providers |
| `SysDiff.Reporting` | Console, JSON, Markdown и HTML |
| `SysDiff.ProviderSdk` | явный контракт внешних providers |
| `SysDiff.Cli` | DI, command routers, TUI, watch/live, updater |

## Versioned command routers

```text
V8CommandRouter
  ├── compatibility status|matrix|inspect|verify
  └── V7CommandRouter
        ├── update check|status|download|install|settings|clear-cache
        └── V6CommandRouter
              ├── baseline
              ├── drift
              ├── timeline
              ├── case
              └── V4 → V3 → CommandApp
```

Новая версия перехватывает только свои команды и делегирует остальные вниз. Это уменьшает риск регрессии parser logic и сохраняет старые automation scripts.

## Snapshot Archive Compatibility

### Экспорт

`SnapshotArchiveService.ExportAsync` создаёт:

```text
snapshot.sdshot
├── manifest.json
├── snapshot.json
└── checksums.sha256
```

Manifest содержит format identifier, container format version, snapshot schema version, producer version, Snapshot ID и время создания.

### Inspection

`InspectAsync` является read-only операцией:

1. проверяет размер файла;
2. открывает ZIP с лимитом entries;
3. валидирует каждый entry path;
4. требует единственный manifest, snapshot и checksum;
5. проверяет uncompressed size;
6. сверяет точные SHA-256 строки;
7. десериализует JSON;
8. проверяет format, Snapshot ID и schema invariants;
9. возвращает `SnapshotArchiveInspection`;
10. не вызывает `ISnapshotStore.SaveSnapshotAsync`.

### Import

`ImportAsync` использует ту же policy evaluation. Сохранение выполняется только при `CanImport=true`; newer, legacy-without-handler и invalid archives не создают частичный snapshot.

## Compatibility model

```text
Compatible
RequiresNewerSysDiff
UnsupportedLegacy
Invalid
```

Текущая матрица:

```text
container format: 1..1
snapshot schema:  1..1
```

Эта матрица описывает текущий reader, но ещё не является публичной schema 1.0.

## Storage и migrations

`SqliteSnapshotStore` сохраняет core snapshots/comparisons. `SqliteInvestigationStore` добавляет additive investigation tables и `app_migrations`.

0.8.0 не изменяет SQLite schema. Будущий migration handler должен быть последовательным, идемпотентным, транзакционным, иметь backup/dry-run и отклонять неизвестную более новую схему.

## Release Channel

`UpdateService` проверяет официальный stable manifest, HTTPS allow-list, size и SHA-256. `UpdateInstaller` выполняет staging, version verification, backup, replace, post-install verification и rollback.

Release workflow после squash merge ветки `agent/sysdiff-vX.Y.Z` повторяет tests/package/smoke, создаёт tag, provenance attestations и GitHub Release.

## Безопасность

- providers выполняют только заранее определённое чтение;
- captured paths, commands, notes и tags остаются данными;
- plugin DLL загружается только через явный `--plugin`;
- `.sdshot` не доверяет ZIP names, declared size или checksum text;
- newer snapshot не десериализуется с последующим сохранением урезанной модели;
- inspection не меняет SQLite, baseline или active case;
- updater не заменяет работающий EXE напрямую;
- опасные действия требуют явного подтверждения.

## Тестирование

- Core tests: comparison, privacy, profiles, archive round-trip и compatibility;
- Providers tests: severity/noise и Windows provider behavior;
- CLI tests: routers, TUI, updater, manifest и installer plan;
- integration tests: SQLite initialization и investigation persistence;
- smoke-test: version, help, compatibility matrix, doctor, timeline, cases, updater и TUI frame;
- release workflow: self-contained `win-x64`, package manifest, SHA-256 и release assets.
