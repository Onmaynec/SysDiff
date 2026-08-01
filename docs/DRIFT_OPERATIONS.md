# 🧭 Drift Operations

SysDiff 0.6.0 добавляет постоянную точку доверия и case-based workflow для регулярного контроля Windows.

## Модель

```text
BaselineRecord
      │
      ├── SnapshotRecord (trusted)
      │
      ▼
Drift Scan ── current SnapshotRecord
      │
      ├── ComparisonResult
      ├── DriftRiskSummary
      ├── HTML / JSON
      ├── TimelineEventRecord
      └── active InvestigationCaseRecord
```

Все операции локальные. Baseline и risk score ничего не изменяют в системе.

## Baseline Vault

Baseline хранит:

- ID доверенного snapshot;
- отображаемое имя;
- время закрепления;
- необязательную заметку.

```powershell
sysdiff baseline set trusted-clean
sysdiff baseline set trusted-clean --note "После обновления Windows"
sysdiff baseline show
sysdiff baseline clear
```

Ограничения:

- `Failed`, `Cancelled` и `Corrupted` snapshots отклоняются;
- `Partial` разрешён с предупреждением;
- удалённый snapshot делает baseline недействительной;
- `clear` снимает указатель, но не удаляет snapshot.

## Drift Scan

```powershell
sysdiff drift scan --profile standard --noise Balanced
```

Этапы:

1. загрузка baseline;
2. создание текущего snapshot;
3. comparison;
4. сохранение comparison в SQLite;
5. расчёт risk score;
6. создание HTML и JSON;
7. запись Timeline event;
8. привязка к активному case.

Если current snapshot частичный, команда возвращает код `7`, но сохраняет доступный результат.

## Explainable Risk Score

Вес одного изменения зависит от:

- severity;
- confidence;
- noise marker;
- количества затронутых providers.

Базовые веса:

| Severity | Вес |
|---|---:|
| Info | 0.5 |
| Low | 2 |
| Medium | 6 |
| High | 15 |
| Critical | 30 |

Confidence ограничивается диапазоном `0.25–1.0`. Noise change получает множитель `0.25`. Provider diversity добавляет не более 10 пунктов. Итог ограничен `0–100`.

| Score | Level |
|---:|---|
| 0–4 | Stable |
| 5–14 | Notice |
| 15–34 | Elevated |
| 35–64 | High |
| 65–100 | Critical |

Score не является вероятностью malware. Это приоритет анализа изменений.

## Investigation Timeline

```powershell
sysdiff timeline list
sysdiff timeline list --limit 100
sysdiff timeline list --kind DriftScan
```

Поддерживаемые типы:

- `Snapshot`;
- `Comparison`;
- `DriftScan`;
- `Report`;
- `Case`;
- `Note`.

Timeline реконструирует старые snapshots/comparisons прямо из существующих таблиц. Никакой rewrite данных не выполняется.

## Case Vault

```powershell
sysdiff case create "Browser test" --description "Проверка установщика" --tags browser,installer
sysdiff case list
sysdiff case show "Browser test"
sysdiff case use "Browser test"
sysdiff case close "Browser test"
sysdiff case use none
```

Активный case автоматически получает links на:

- current snapshot;
- comparison;
- HTML report;
- baseline при её установке.

Закрытый case нельзя сделать активным. Закрытие не удаляет links, snapshots или reports.

## SQLite migration

0.6.0 создаёт:

```sql
app_migrations
investigation_settings
investigation_cases
investigation_links
timeline_events
```

Migration:

- additive;
- idempotent;
- не меняет legacy tables;
- фиксируется записью `0.6.0-investigations`;
- использует foreign keys только между новыми case/timeline tables.

## Безопасность

- notes и tags не выполняются;
- report paths не запускаются автоматически из CLI;
- Drift Scan read-only относительно исследуемой системы;
- baseline не является rollback point;
- результаты следует проверять по фактическим changes и provider warnings.
