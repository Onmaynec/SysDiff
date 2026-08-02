# ⌨️ Команды SysDiff 0.11.0

## Общие команды

```powershell
sysdiff
sysdiff --help
sysdiff --version
sysdiff doctor
sysdiff --tui-smoke
```

## Legacy Bridge

### Matrix

```powershell
sysdiff legacy matrix
sysdiff legacy matrix --json
```

Aliases верхнего уровня: `upgrade`, `bridge`.

### Read-only plan и status

```powershell
sysdiff legacy status comparison .\report-old.json
sysdiff legacy plan comparison .\report-old.json --json
sysdiff legacy status bundle .\investigation-old.zip
sysdiff legacy plan bundle .\investigation-old.zip --json
```

Статусы: `Current`, `UpgradeAvailable`, `RequiresNewerSysDiff`, `UnsupportedLegacy`, `Invalid`.

### Verify

```powershell
sysdiff legacy verify comparison .\report-v1.json
sysdiff legacy verify bundle .\investigation-v1.zip --json
```

`verify` возвращает exit code `0` только для `Current`. Файл, который можно преобразовать, но ещё не преобразован, возвращает `4`.

### Convert

```powershell
sysdiff legacy convert comparison .\report-old.json --yes
sysdiff legacy convert comparison .\report-old.json `
  --output .\report-v1.json --yes
sysdiff legacy convert bundle .\investigation-old.zip --yes --json
```

Без `--output` создаётся `<name>.schema-v1.<ext>`. Existing output требует `--overwrite`; conversion всегда требует `--yes` и создаёт backup исходника.

In-place conversion:

```powershell
sysdiff legacy convert comparison .\report-old.json `
  --output .\report-old.json --overwrite --yes
```

## Schema Contract Center

### Каталог и matrix

```powershell
sysdiff schema list
sysdiff schema list --json
sysdiff schema matrix
sysdiff schemas catalog --json
```

Каталог содержит product version, contract major, JSON Schema draft, `$id`, file name и compatibility policy.

### Получить embedded schema

```powershell
sysdiff schema show snapshot
sysdiff schema show comparison
sysdiff schema show bundle
```

### Проверить документ

```powershell
sysdiff schema validate snapshot .\snapshot.json
sysdiff schema validate snapshot .\snapshot.json --json
sysdiff schema validate comparison .\report.json --json
sysdiff schema validate bundle .\manifest.json --json
sysdiff schema verify snapshot .\snapshot.json
sysdiff contract verify bundle .\manifest.json
```

Статусы: `Valid`, `Invalid`, `RequiresNewerSysDiff`. Unknown additive properties разрешены. Invalid/future schema возвращает exit code `4`.

## Migration Lab

```powershell
sysdiff migration status
sysdiff migration status --json
sysdiff migration plan
sysdiff migration plan --json
sysdiff migration history
sysdiff migration history --json
sysdiff migration apply --yes
sysdiff migration apply --yes --json
```

`status` и `plan` read-only. Apply требует `--yes`, создаёт SQLite-consistent backup и выполняет каждый migration step в transaction. Ошибка возвращает exit code `9`.

## Compatibility Center

```powershell
sysdiff compatibility status
sysdiff compatibility status --json
sysdiff compatibility matrix
sysdiff compatibility inspect .\before.sdshot
sysdiff compatibility inspect .\before.sdshot --json
sysdiff compatibility verify .\before.sdshot
```

Inspection проверяет ZIP paths, обязательные entries, SHA-256, format, Snapshot ID и schema version без записи в SQLite.

## Обновления

```powershell
sysdiff update check
sysdiff update check --json
sysdiff update status
sysdiff update status --json
sysdiff update download
sysdiff update download --json
sysdiff update install --yes
sysdiff update install --yes --restart
sysdiff update settings
sysdiff update settings --auto-check true --interval-hours 24
sysdiff update settings --auto-download false
sysdiff update settings --ignore 0.11.0
sysdiff update settings --ignore none
sysdiff update clear-cache
```

Auto-install отсутствует. Установка всегда требует подтверждения.

## Baseline, drift, timeline и cases

```powershell
sysdiff baseline show
sysdiff baseline set <snapshot-name-or-id> --note "trusted"
sysdiff baseline clear

sysdiff drift scan --profile standard --noise Balanced
sysdiff timeline list --limit 100
sysdiff timeline list --kind DriftScan

sysdiff case create "Installer audit" --tags installer,test
sysdiff case list
sysdiff case show <name-or-id>
sysdiff case use <name-or-id>
sysdiff case use none
sysdiff case close <name-or-id>
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

Перед import внешнего архива используйте `compatibility inspect`. `.sdshot` 0.3–0.9 не требует Legacy Bridge, потому что snapshot schema остаётся `1`.

## Сравнение и reports

```powershell
sysdiff compare <before> <after>
sysdiff compare before after --noise Raw
sysdiff compare before after --severity High
sysdiff compare before after --format html --output .\report.html
sysdiff compare before after --format json --output .\report.json
sysdiff compare pc-a pc-b --cross-machine
```

JSON report 0.10+ соответствует comparison Schema Contract v1. Reports 0.3–0.9 преобразуются через Legacy Bridge.

## Watch и Live Monitor

```powershell
sysdiff watch .\Setup.exe
sysdiff watch .\Setup.exe --arguments "/S"
sysdiff watch .\Setup.exe --wait-for-children --timeout 900
sysdiff watch --no-launch

sysdiff live process --duration 60
sysdiff live process --duration 120 --root-pid 1234 --format markdown
sysdiff live network --duration 60 --format json
```

## Investigation bundle

```powershell
sysdiff bundle create <comparison-id>
sysdiff bundle create <comparison-id> --output .\investigation.zip
```

Bundle 0.10+ self-validates against public schema v1. Bundle 0.3–0.9 можно преобразовать через `legacy convert bundle`.

## Профили и плагины

```powershell
sysdiff profile list
sysdiff profile show standard
sysdiff profile load .\profile.json
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
| 0 | успех / current / upgrade plan available |
| 1 | общая, сетевая или update-ошибка |
| 2 | некорректные аргументы, отсутствует `--yes` или TUI unavailable |
| 3 | snapshot/baseline не найдены |
| 4 | legacy/schema invalid, future, unsupported или несовместимый portable format |
| 5 | доступ запрещён |
| 7 | partial snapshot или безопасный timeout |
| 8 | отменено |
| 9 | storage, future DB или migration error |
