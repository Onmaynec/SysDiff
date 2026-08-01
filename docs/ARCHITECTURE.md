# 🏗️ Архитектура SysDiff 0.6.0

## Цель

SysDiff отделяет сбор Windows-данных от управления, хранения и визуализации. Cyber Console и non-interactive CLI используют одинаковые application services. Анимации не участвуют в системных операциях.

## Слои

```text
Cyber Control Node                  Non-interactive CLI
        │                                      │
        └────────────────┬─────────────────────┘
                         ▼
               V6 → V4 → V3 routers
                         │
        ┌────────────────┼───────────────────┐
        ▼                ▼                   ▼
Snapshot workflows   Drift Operations    Live/Watch workflows
        │                │                   │
        ▼                ▼                   ▼
SnapshotCoordinator  DriftOperationsService Process/Network monitors
        │                │
        ▼                ├── ComparisonEngine
ISnapshotProvider[]      ├── DriftRiskEngine
        │                ├── Reporting
        └────────┬───────┴──────────────┐
                 ▼                      ▼
        ISnapshotStore          IInvestigationStore
                 │                      │
                 └──────── SQLite sysdiff.db ────────┘
```

## Domain

`Investigations.cs` содержит:

- `BaselineRecord`;
- `InvestigationCaseRecord`;
- `InvestigationLink`;
- `TimelineEventRecord`;
- `DriftRiskSummary`;
- `DriftScanResult`;
- `IInvestigationStore`.

Эти модели не зависят от Console, SQLite или Windows API.

## Storage

### `SqliteSnapshotStore`

Сохраняет legacy core data:

```text
snapshots
snapshot_providers
artifacts
comparisons
changes
```

### `SqliteInvestigationStore`

Добавляет 0.6 tables:

```text
app_migrations
investigation_settings
investigation_cases
investigation_links
timeline_events
```

Migration additive и idempotent. Существующие таблицы не изменяются.

`ListTimelineAsync` объединяет explicit timeline events с реконструированными snapshots/comparisons. Реконструкция read-only и не переписывает старые записи.

## Drift Operations

### Baseline

`DriftOperationsService.SetBaselineAsync`:

1. загружает snapshot;
2. проверяет статус;
3. сохраняет `BaselineRecord`;
4. записывает Timeline note;
5. связывает baseline с active case.

### Scan

`ScanAsync`:

1. загружает baseline;
2. создаёт current snapshot через `SnapshotCoordinator`;
3. запускает `ComparisonEngine`;
4. сохраняет comparison;
5. рассчитывает `DriftRiskSummary`;
6. пишет HTML и JSON;
7. создаёт Timeline event;
8. добавляет links в active case.

Ни один этап не выполняет rollback или изменение исследуемой системы.

## Drift Risk Engine

Формула детерминирована:

```text
sum(severityWeight × confidence × noiseMultiplier)
+ providerDiversityBonus
= clamp(0..100)
```

Risk score является приоритетом анализа, не malware probability.

## CLI routers

```text
V6CommandRouter
  ├── baseline
  ├── drift
  ├── timeline
  ├── case
  └── V4CommandRouter
        └── V3CommandRouter
              └── CommandApp
```

Так команды 0.1–0.5 сохраняются без копирования parser logic.

## Cyber Console

`TerminalControlCenter` разделён на partial files:

- основной Snapshot/Diff/Watch/Live workflow;
- `TerminalControlCenter.Drift.cs` для baseline, timeline и cases.

Главное меню содержит девять модулей. `System Node` группирует diagnostics/settings/about/disconnect.

`TerminalRenderer.Drift.cs` визуализирует score, levels, severity distribution, factors и report paths.

## Совместимость и безопасность

- unknown notes/tags остаются строками;
- captured paths не выполняются;
- удалённая baseline возвращает `null`;
- закрытие case не удаляет linked objects;
- active case хранится отдельной setting;
- foreign keys существуют только между новыми tables;
- JSON identifiers не зависят от TUI colors или языка.

## Тестирование

- Core tests проверяют score и boundary levels;
- CLI tests проверяют Command Deck;
- integration tests создают реальную SQLite database;
- repeated initialization проверяет idempotency;
- smoke-test проверяет version/help/timeline/cases/TUI frame;
- Windows CI публикует self-contained `win-x64`.
