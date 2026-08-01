# 🏗️ Архитектура SysDiff 0.5.0

## Цель

SysDiff отделяет сбор и анализ Windows-данных от способа управления. Cyber Console и классический CLI используют одни и те же Domain/Core/Storage/Providers/Reporting services. Анимации являются только представлением и не участвуют в системных операциях.

## Слои

```text
Cyber Control Node             Non-interactive CLI
        │                               │
        └────────────┬──────────────────┘
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

## Compatibility chain

`V4CommandRouter → V3CommandRouter → CommandApp` сохраняет команды версий 0.1–0.4. Текущий router перехватывает только:

- запуск без аргументов;
- `--version`;
- `--tui-smoke`;
- дополнение `--help`.

Все остальные команды проходят в прежние маршруты без изменения машинного вывода.

## Cyber Console

### `TerminalControlCenter`

Координирует интерактивные модули:

- Snapshot Node;
- Diff Lab и Change Explorer;
- Watch Operations;
- Live Signal Monitor;
- Report Vault;
- Node Diagnostics;
- Settings/About.

Класс вызывает существующие application services напрямую и не дублирует storage/provider logic.

### `TerminalRenderer`

Отвечает за представление:

- Cyber Control Node dashboard;
- boot sequence;
- Command Deck;
- Action Console;
- Provider Stream;
- wide/compact layouts;
- prompts, confirmations и result panels;
- восстановление состояния консоли.

`RunSpinnerAsync` является общей точкой визуализации длительных операций. Он принимает обычную `Task`, отображает состояние и возвращает исходный результат без изменения.

### `CyberTheme`

Централизует:

- product name и version;
- foreground/background palette;
- success/warning/error colors;
- stage markers `[OK]`, `[>>]`, `[--]`, `[!!]`, `[XX]`, `[//]`;
- системные badges.

Даже при `NO_COLOR` смысл сохраняется текстовыми маркерами.

### `CyberAnimation`

Чистый генератор кадров:

- spinner;
- pulse;
- progress bar;
- scanner line;
- reveal helpers.

Методы не обращаются к Console, Windows API или application services и покрываются unit tests.

### `TerminalMotionPolicy`

Определяет допустимость движения и цветов на основе:

- interactive capability;
- redirected output;
- переменной `CI`;
- `SYSDIFF_NO_ANIMATIONS`;
- `NO_COLOR`.

Политика создаётся один раз при построении renderer. Поэтому смена переменной во время операции не приводит к частично перерисованному экрану.

### `TerminalMenuNavigator`

Чистая модель навигации. Реализует:

- wrap-around для `↑/↓`;
- `Home`/`End`;
- стандартные действия;
- direct module selection через `1–9`;
- dashboard aliases `P/B/A`, `C`, `W`, `L`, `D`.

Letter aliases активируются только для главного меню из девяти модулей и не конфликтуют с внутренними списками.

### `TerminalCapabilities`

TUI запрещён, когда:

- `stdin` перенаправлен;
- `stdout` перенаправлен;
- процесс не является интерактивным.

Это сохраняет чистый вывод в CI, pipes и PowerShell automation.

## Action Console

```text
Application operation Task<T>
             │
             ▼
       RunSpinnerAsync<T>
             │
      ┌──────┼─────────┐
      ▼      ▼         ▼
  running  completed  failed/cancelled
      │      │         │
      └──────┴─────────┘
             ▼
     original result/exception
```

Action Console не перехватывает и не преобразует payload операции. Исключение и cancellation продолжают передаваться вызывающему коду.

## Provider Stream

Snapshot capture передаёт `IProgress<SnapshotProgress>`. Renderer ограничивает частоту кадров и показывает:

- provider ID;
- processed count;
- provider message;
- current item.

Throttling действует только на отображение. Координатор и providers получают все события без потери данных снимка.

## Состояние консоли

`ConsoleSession` реализует `IDisposable` и восстанавливает foreground/background colors и видимость курсора. Внешние исключения обрабатываются в `Program`, после чего session уже освобождена.

## Тестирование

- Core/Providers tests проверяют анализ;
- CLI tests проверяют навигацию и interactive capability;
- Cyber Console tests проверяют hotkeys, markers, progress/scanner frames и motion policy;
- `--tui-smoke` проверяет статический кадр без клавиатуры и задержек;
- Windows build публикует self-contained `win-x64` и запускает smoke-test версии/справки/diagnostics/Cyber Console.
