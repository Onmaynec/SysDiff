# 🟢 SysDiff Cyber Console

SysDiff 0.5.0 запускает полноэкранный **Cyber Control Node**, когда команда вызвана без аргументов в обычном интерактивном терминале:

```powershell
sysdiff
```

CLI-команды с аргументами не открывают панель и сохраняют стабильный текстовый вывод для PowerShell, скриптов и CI.

## Концепция

Интерфейс построен по модели самостоятельной системной утилиты и визуально приближен к NexRoute:

- крупный ASCII-логотип;
- чёрный фон и neon green/cyan palette;
- нумерованные модули `[01]`…`[09]`;
- системные badges и телеметрия узла;
- Command Deck для быстрого запуска;
- отдельная Action Console для длительных операций;
- живой Provider Stream во время снимка;
- маркеры состояния, читаемые даже без цвета.

## Главный экран

```text
┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
┃                        SYSDIFF CYBER CONSOLE                        ┃
┃              WINDOWS INVESTIGATION CONTROL NODE // 0.5.0           ┃
┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┫
┃ [ NODE:ONLINE ] [ ROOT:ADMIN ] [ OS:10.0.26100 ] [ ARCH:X64 ]       ┃
┃ [ SNAPSHOTS:4 ] [ REPORTS:7 ] [ PROVIDERS:11 ] [ STORAGE:PROFILE ]  ┃
┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┫
┃ ▶ [01] ◆ SNAPSHOT NODE       // capture, browse and transfer        ┃
┃   [02] ◇ DIFF LAB            // compare and investigate             ┃
┃   [03] ▶ WATCH OPERATIONS    // controlled program session          ┃
┃   [04] ● LIVE SIGNAL         // processes and network endpoints     ┃
┃   [05] ▤ REPORT VAULT        // reports and investigation bundles   ┃
┃   [06] ✓ NODE DIAGNOSTICS    // Windows, rights, SQLite, providers  ┃
┃   [07] ⚙ SYSTEM SETTINGS     // paths, motion and colors            ┃
┃   [08] i ABOUT               // version, purpose and security       ┃
┃   [09] × DISCONNECT          // close the local control node        ┃
┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┫
┃ QUICK OPS > 1-9 · P/B/A SNAPSHOT · C DIFF · W WATCH · L LIVE · D   ┃
┃ NAV > ↑↓ MOVE · ENTER EXECUTE · ESC BACK · F5 RESCAN · Q EXIT      ┃
┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛
```

Если ширина окна меньше 96 символов, панель автоматически использует compact layout.

## Command Deck

| Клавиша | Действие |
|---|---|
| `1`…`9` | открыть соответствующий модуль |
| `P`, `B`, `A` | Snapshot Node |
| `C` | Diff Lab |
| `W` | Watch Operations |
| `L` | Live Signal Monitor |
| `D` | Node Diagnostics |
| `↑` / `↓` | перемещение по меню или списку |
| `Home` / `End` | первый или последний элемент |
| `Enter` | открыть или подтвердить |
| `Esc` | назад |
| `Q` | выход или закрытие текущего browser |
| `F5` | повторно прочитать состояние dashboard |

В Change Explorer дополнительно используются `/`, `F`, `S`, `R` и `E`.

## Boot sequence

Короткая анимация выполняется только при интерактивном запуске:

1. terminal channel;
2. local storage;
3. snapshot providers;
4. comparison engine;
5. live monitors;
6. control node online.

Boot sequence является только визуальным представлением. Он не запускает дополнительные проверки, не изменяет систему и может быть пропущен любой клавишей.

По умолчанию вся последовательность занимает меньше полутора секунд.

## Action Console

Все операции, которые раньше показывали одиночный spinner, используют общую Action Console:

```text
[OK] CONTROL CHANNEL
[>>] FORMING INVESTIGATION BUNDLE
[--] COMMIT RESULT

STREAM █████████████▓▒░░░░░░  64%
SCAN   ·······▓████··········
TRACE  > packing snapshots, comparison and reports
```

Панель показывает:

- состояние этапов `queued/running/completed/failed/cancelled`;
- elapsed time;
- PID процесса SysDiff;
- progress и scanner bars;
- текущую операцию;
- итоговый результат;
- подсказку `Ctrl+C` для отмены.

Action Console применяется к импорту/экспорту `.sdshot`, формированию отчётов, investigation bundle, ожиданию процессов, stabilization delay, live monitor и другим длительным действиям.

## Provider Stream

При создании снимка отображаются:

- активный provider;
- число обработанных артефактов;
- текущее сообщение provider;
- путь или системный объект;
- будущий этап SQLite commit.

Обновление кадра ограничено по частоте, поэтому быстрый provider не создаёт тысячи строк в истории CMD.

## Цвета и маркеры

| Значение | Цвет | Текстовый маркер |
|---|---|---|
| успешно | neon green | `[OK]` |
| выполняется | cyan | `[>>]` |
| ожидает | dark gray | `[--]` |
| предупреждение | amber | `[!!]` |
| ошибка | red | `[XX]` |
| отменено | amber | `[//]` |

Маркеры сохраняют смысл при отключённых цветах.

## Safe animation mode

Полностью отключить движение:

```powershell
$env:SYSDIFF_NO_ANIMATIONS = "1"
sysdiff
```

Отключить цвета:

```powershell
$env:NO_COLOR = "1"
sysdiff
```

Значения `1`, `true`, `yes` и `on` считаются включёнными.

Анимации автоматически отключаются:

- в CI;
- при redirected stdout;
- при неинтерактивном запуске;
- когда `SYSDIFF_NO_ANIMATIONS` включён.

## Состояние консоли

`ConsoleSession` сохраняет и восстанавливает:

- цвет текста;
- цвет фона;
- видимость курсора;
- заголовок и очищенный экран при завершении.

Восстановление выполняется через `IDisposable`, включая выход после исключения или отмены.

## Совместимость

Поддерживаются:

- CMD;
- Windows PowerShell 5.1;
- PowerShell 7;
- Windows Terminal.

Рекомендуемый размер окна — не менее `105×30`. На меньшем размере используется compact layout, а длинные пути безопасно сокращаются.
