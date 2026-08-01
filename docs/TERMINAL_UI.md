# 🟢 SysDiff Cyber Console 0.6.0

SysDiff без аргументов открывает полноэкранный **Windows Drift Investigation Node**:

```powershell
sysdiff
```

CLI-команды с аргументами сохраняют стабильный текстовый вывод для PowerShell, скриптов и CI.

## Dashboard

Широкий режим показывает:

```text
SYSDIFF CYBER CONSOLE
WINDOWS DRIFT INVESTIGATION NODE // BUILD 0.6.0

[ NODE:ONLINE ] [ ROOT:ADMIN ] [ OS:10.0.26100 ] [ ARCH:X64 ]
[ SNAPSHOTS:12 ] [ REPORTS:8 ] [ PROVIDERS:11 ] [ STORAGE:PROFILE ]
DATA CHANNEL  > C:\Users\...\AppData\Local\SysDiff
DRIFT CHANNEL > BASELINE:trusted-clean // CASE:Installer audit // RISK:027/100
```

При ширине меньше 96 символов включается compact mode.

## Девять модулей

| № | Модуль | Назначение |
|---:|---|---|
| 01 | Snapshot Node | snapshots и `.sdshot` |
| 02 | Diff Lab | comparisons и Change Explorer |
| 03 | Drift Operations | baseline, scan и risk summary |
| 04 | Investigation Timeline | хронология событий |
| 05 | Case Vault | cases, tags и links |
| 06 | Watch Operations | before/after workflow |
| 07 | Live Signal | process/network monitor |
| 08 | Report Vault | reports и bundles |
| 09 | System Node | diagnostics, settings, about, exit |

## Command Deck

| Клавиша | Действие |
|---|---|
| `1`…`9` | открыть модуль |
| `P`, `B`, `A` | Snapshot Node |
| `C` | Diff Lab |
| `G` | Drift Operations |
| `T` | Investigation Timeline |
| `K` | Case Vault |
| `W` | Watch Operations |
| `L` | Live Signal |
| `D` | System Node |
| `↑` / `↓` | навигация |
| `Home` / `End` | первый/последний пункт |
| `Enter` | выполнить |
| `Esc` | назад |
| `F5` | обновить dashboard |
| `Q` | disconnect |

Change Explorer дополнительно использует `/`, `F`, `S`, `R`, `E`.

## Drift Operations

Экран всегда показывает активную baseline и active case.

### Run Drift Scan

Пользователь выбирает:

1. profile;
2. noise mode;
3. подтверждение resource-heavy full profile.

Provider Stream показывает текущий provider, число объектов и active path. После comparison открывается risk panel:

```text
RISK CHANNEL // 027/100 // ELEVATED
[████████░░░░░░░░░░░░░░░░░░░░]
Changes             18
Severity High        1
Severity Medium      4
```

### Baseline Vault

- show;
- set/replace;
- clear;
- explicit warning для partial snapshot.

### Investigation Timeline

Фильтры:

- All;
- DriftScan;
- Snapshot;
- Comparison;
- Case;
- Note.

Выбранный event показывает timestamp, status, severity, reference, case и metadata.

### Case Vault

- create;
- browse;
- make active;
- close;
- clear active case.

Закрытие case не удаляет snapshots/reports.

## System Node

System Node группирует:

- Node Diagnostics;
- System Settings;
- About Node;
- Disconnect.

Это сохраняет девять основных модулей и цифровой Command Deck.

## Анимации

- boot sequence;
- Action Console;
- Provider Stream;
- progress/scanner bars;
- elapsed time;
- status markers.

Отключение:

```powershell
$env:SYSDIFF_NO_ANIMATIONS = "1"
$env:NO_COLOR = "1"
sysdiff
```

При redirected input/output анимации не запускаются.

## Состояние Console

`ConsoleSession` сохраняет и восстанавливает:

- foreground/background;
- cursor visibility;
- console title;
- screen state при штатном выходе.

Ошибки Cursor API обрабатываются без аварийного завершения.

## Совместимость

Поддерживаются:

- CMD;
- Windows PowerShell 5.1;
- PowerShell 7;
- Windows Terminal.

Рекомендуемый размер — `110×30` или больше. Unicode-шрифты: Cascadia Mono, Cascadia Code, Consolas.
