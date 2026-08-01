# ⌨️ Команды SysDiff 0.4.0

## Terminal Control Center

```powershell
sysdiff
```

Без аргументов SysDiff открывает полноэкранную интерактивную панель. Управление: `↑`, `↓`, `Enter`, `Esc`, `Q`. Полный список горячих клавиш: [TERMINAL_UI.md](TERMINAL_UI.md).

## Общие команды

```powershell
sysdiff --help
sysdiff --version
sysdiff doctor
```

`--tui-smoke` предназначен для CI и выводит статический preview панели без чтения клавиш:

```powershell
sysdiff --tui-smoke
```

## Снимки

```powershell
sysdiff snapshot create <name>
sysdiff snapshot create before --profile minimal
sysdiff snapshot create before --profile standard
sysdiff snapshot create before --profile full --yes
sysdiff snapshot create custom --profile-file .\profile.json
sysdiff snapshot list
sysdiff snapshot show <name-or-id>
sysdiff snapshot delete <name-or-id> --yes
sysdiff snapshot export <name-or-id> --output .\before.sdshot
sysdiff snapshot import .\before.sdshot
```

## Сравнение

```powershell
sysdiff compare <before> <after>
sysdiff compare before after --noise Raw
sysdiff compare before after --severity High
sysdiff compare before after --format html --output .\report.html
sysdiff compare pc-a pc-b --cross-machine
```

| Параметр | Значения | Назначение |
|---|---|---|
| `--noise` | `Raw`, `Balanced`, `Strict` | фильтрация шума |
| `--severity` | `Info`…`Critical` | минимальная важность |
| `--format` | `console`, `json`, `html`, `markdown` | формат отчёта |
| `--output` | путь | выходной файл |
| `--cross-machine` | флаг | явное сравнение разных компьютеров |

## Watch

```powershell
sysdiff watch .\Setup.exe
sysdiff watch .\Setup.exe --arguments "/S"
sysdiff watch .\Setup.exe --wait-for-children --timeout 900
sysdiff watch .\Setup.exe --stabilization-delay 5 --noise Strict
sysdiff watch --no-launch
```

SysDiff не завершает процессы при тайм-ауте.

## Live Monitor

```powershell
sysdiff live process --duration 60
sysdiff live process --duration 120 --root-pid 1234 --format markdown
sysdiff live network --duration 60 --format json
```

## Investigation bundle

```powershell
sysdiff bundle create <comparison-id>
sysdiff bundle create <comparison-id> --output .\investigation.zip
```

## Профили и плагины

```powershell
sysdiff profile list
sysdiff profile show standard
sysdiff profile load .\profile.json
sysdiff snapshot create custom --profile-file .\profile.json
sysdiff snapshot create plugin-test --plugin C:\Plugins\Example.dll
```

Плагин является исполняемым кодом и загружается только по явному `--plugin`.

## Конфигурация и пути

```powershell
sysdiff config show
sysdiff config path
```

## Коды завершения

| Код | Значение |
|---:|---|
| 0 | успех |
| 1 | общая ошибка |
| 2 | некорректные аргументы или невозможность запустить TUI в redirected режиме |
| 3 | снимок не найден |
| 5 | доступ запрещён |
| 7 | частичный снимок или безопасно завершённый timeout |
| 8 | отменено |
| 9 | ошибка хранилища |
