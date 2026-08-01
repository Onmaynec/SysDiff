# ⌨️ Команды SysDiff 0.8.0

## Cyber Console

```powershell
sysdiff
```

Без аргументов SysDiff открывает Cyber Control Node. В `[09] System Node` доступен **Update Center**. Подробности: [TERMINAL_UI.md](TERMINAL_UI.md) и [UPDATES.md](UPDATES.md).

### Command Deck

| Клавиша | Назначение |
|---|---|
| `1`…`9` | открыть модуль по номеру |
| `P`, `B`, `A` | Snapshot Node |
| `C` | Diff Lab |
| `G` | Drift Operations |
| `T` | Investigation Timeline |
| `K` | Case Vault |
| `W` | Watch Operations |
| `L` | Live Signal |
| `D` | System Node |
| `F5` | обновить dashboard |
| `Q` | выход |

## Общие команды

```powershell
sysdiff --help
sysdiff --version
sysdiff doctor
sysdiff --tui-smoke
```

## Compatibility Center

### Матрица reader

```powershell
sysdiff compatibility status
sysdiff compatibility status --json
sysdiff compatibility matrix
sysdiff compat matrix --json
```

Команда показывает текущий container format, snapshot schema и минимальные читаемые версии.

### Проверка `.sdshot`

```powershell
sysdiff compatibility inspect .\before.sdshot
sysdiff compatibility inspect .\before.sdshot --json
sysdiff compatibility verify .\before.sdshot
sysdiff compat verify .\before.sdshot --json
```

Inspection проверяет ZIP paths, обязательные entries, SHA-256, JSON, format identifier, Snapshot ID и schema version. Операция не сохраняет snapshot в SQLite.

Статусы: `Compatible`, `RequiresNewerSysDiff`, `UnsupportedLegacy`, `Invalid`. Несовместимый или повреждённый архив возвращает exit code `4`.

## Обновления

### Проверка и состояние

```powershell
sysdiff update check
sysdiff update check --json
sysdiff update status
sysdiff update status --json
```

`update check` выполняет сетевой запрос к официальному stable manifest. `update status` показывает сохранённое состояние без обязательного сетевого обращения.

### Загрузка и установка

```powershell
sysdiff update download
sysdiff update download --json
sysdiff update install --yes
sysdiff update install --yes --restart
```

`update download` проверяет host/path, размер, SHA-256, ZIP paths и staged EXE version. `update install` доступна только для опубликованного `sysdiff.exe`; при `dotnet run` установка отклоняется. Установка всегда требует `--yes`.

### Настройки

```powershell
sysdiff update settings
sysdiff update settings --json
sysdiff update settings --auto-check true
sysdiff update settings --auto-check false
sysdiff update settings --auto-download true
sysdiff update settings --auto-download false
sysdiff update settings --interval-hours 12
sysdiff update settings --ignore 0.9.0
sysdiff update settings --ignore none
sysdiff update clear-cache
```

| Параметр | Значения | По умолчанию |
|---|---|---|
| `--auto-check` | `true`, `false` | `true` |
| `--auto-download` | `true`, `false` | `false` |
| `--interval-hours` | `1–168` | `24` |
| `--ignore` | SemVer или `none` | `none` |

Auto-download не устанавливает обновление. Auto-install отсутствует.

## Baseline

```powershell
sysdiff baseline show
sysdiff baseline set <snapshot-name-or-id>
sysdiff baseline set trusted-clean --note "После чистой установки"
sysdiff baseline clear
```

`baseline set` принимает только существующий локальный snapshot. Failed, Cancelled и Corrupted snapshots отклоняются.

## Drift Scan

```powershell
sysdiff drift scan
sysdiff drift scan --profile minimal
sysdiff drift scan --profile standard --noise Balanced
sysdiff drift scan --profile full --noise Raw
```

| Параметр | Значения | По умолчанию |
|---|---|---|
| `--profile` | `minimal`, `standard`, `full` | `standard` |
| `--noise` | `Raw`, `Balanced`, `Strict` | `Balanced` |

Без baseline команда не запускается. Partial current snapshot сохраняется, но команда возвращает код `7`.

## Timeline

```powershell
sysdiff timeline list
sysdiff timeline list --limit 100
sysdiff timeline list --kind Snapshot
sysdiff timeline list --kind Comparison
sysdiff timeline list --kind DriftScan
sysdiff timeline list --kind Case
sysdiff timeline list --kind Note
```

`--limit` принимает значение `1–1000`.

## Case Vault

```powershell
sysdiff case create <name>
sysdiff case create "Installer audit" --description "Проверка Setup.exe" --tags installer,test
sysdiff case list
sysdiff case show <name-or-id>
sysdiff case use <name-or-id>
sysdiff case use none
sysdiff case close <name-or-id>
```

`case create` автоматически делает новый кейс активным. Закрытый кейс нельзя активировать.

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

Перед импортом внешнего архива рекомендуется выполнить `compatibility inspect`.

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

## Конфигурация

```powershell
sysdiff config show
sysdiff config path
```

## Коды завершения

| Код | Значение |
|---:|---|
| 0 | успех |
| 1 | общая, сетевая или update-ошибка |
| 2 | некорректные аргументы или невозможность запустить TUI |
| 3 | snapshot/baseline не найдены |
| 4 | несовместимый или повреждённый переносимый формат |
| 5 | доступ запрещён |
| 7 | частичный snapshot или безопасный timeout |
| 8 | отменено |
| 9 | ошибка хранилища |
