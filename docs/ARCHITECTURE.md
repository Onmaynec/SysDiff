# 🏗️ Архитектура SysDiff 0.4.0

## Цель

SysDiff отделяет сбор и анализ Windows-данных от способа управления. Terminal Control Center и классический CLI используют одни и те же Domain/Core/Storage/Providers/Reporting services.

## Слои

```text
Terminal Control Center        Non-interactive CLI
          │                            │
          └──────────┬─────────────────┘
                     ▼
              Application workflows
                     │
      ┌──────────────┼─────────────────┐
      ▼              ▼                 ▼
SnapshotCoordinator  ComparisonEngine  Live monitors
      │              │                 │
      ▼              ▼                 ▼
ISnapshotProvider[]  Severity/Noise    Process/Network polling
      │
      ▼
SQLite Store ── .sdshot / bundle ── Reporting
```

## Terminal Control Center

### `V4CommandRouter`

Перехватывает только:

- запуск без аргументов;
- `--version`;
- `--tui-smoke`;
- дополнение `--help`.

Все остальные команды делегируются `V3CommandRouter`, поэтому функциональность 0.1–0.3 остаётся совместимой.

### `TerminalControlCenter`

Координирует интерактивные экраны:

- Snapshot Center;
- Comparison Lab;
- Watch Session;
- Live Monitor;
- Reports & Bundles;
- Diagnostics;
- Settings/About.

Класс вызывает существующие application services напрямую и не дублирует storage/provider logic.

### `TerminalRenderer`

Отвечает только за представление:

- ASCII logo;
- wide/compact layout;
- панели и таблицы;
- цветовые статусы;
- prompts со стрелками;
- spinner и progress line;
- восстановление состояния консоли.

### `TerminalMenuNavigator`

Чистая модель навигации, не зависящая от Console. Она покрыта unit tests и реализует wrap-around для `↑/↓`, а также действия hotkeys.

### `TerminalCapabilities`

Решает, можно ли запускать интерактивную панель. TUI запрещён, когда:

- `stdin` перенаправлен;
- `stdout` перенаправлен;
- процесс не является интерактивным.

Это сохраняет чистый вывод CLI в CI и PowerShell pipelines.

## Совместимость

`V4CommandRouter → V3CommandRouter → CommandApp` образует последовательный compatibility chain. Новые версии могут добавлять команды, не переписывая стабильные маршруты предыдущих версий.

## Состояние консоли

`ConsoleSession` реализует `IDisposable` и восстанавливает foreground/background color и видимость курсора. Внешние исключения обрабатываются в `Program`, после чего session уже освобождена.

## Длительные операции

- snapshot capture передаёт `IProgress<SnapshotProgress>`;
- import/export/report/bundle выполняются под spinner;
- Watch Session отображает отдельные этапы;
- logger в interactive launch ограничивается `Error`, чтобы не повреждать layout;
- CLI launch сохраняет обычный уровень логирования.

## Тестирование

- Core/Providers tests проверяют анализ;
- Cli tests проверяют навигацию и interactive capability;
- `--tui-smoke` проверяет создание preview без чтения клавиатуры;
- Windows build публикует self-contained `win-x64` и запускает smoke-test.
