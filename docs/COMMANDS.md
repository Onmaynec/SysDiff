# ⌨️ Команды SysDiff 0.12.0

## Общие команды

```powershell
sysdiff
sysdiff --help
sysdiff --version
sysdiff doctor
sysdiff --tui-smoke
```

## Scale Lab

Aliases верхнего уровня: `large`, `stream`.

### Matrix и limits

```powershell
sysdiff scale matrix
sysdiff scale matrix --json
sysdiff scale limits --json
```

### Synthetic NDJSON

```powershell
sysdiff scale synth .\before.ndjson --count 1000000 --variant before
sysdiff scale synth .\after.ndjson --count 1000000 --variant after --change-every 1000 --json
```

### External sort

```powershell
sysdiff scale sort .\unsorted.ndjson `
  --output .\sorted.ndjson `
  --batch-size 50000 `
  --progress-interval 100000
```

Input сохраняется. Duplicate identities и JSON lines больше 4 MiB отклоняются.

### Streaming comparison

```powershell
sysdiff scale compare .\before.ndjson .\after.ndjson `
  --output .\changes.ndjson

sysdiff scale compare .\before.ndjson .\after.ndjson `
  --output .\all.ndjson `
  --include-unchanged `
  --json
```

Оба input должны быть отсортированы по `identity`. Comparison использует merge-join и пишет changes сразу в output.

### Benchmark

```powershell
sysdiff scale benchmark `
  --output-dir .\ScaleResults `
  --artifacts 1000000 `
  --change-every 1000 `
  --max-managed-mb 256 `
  --min-throughput 1000 `
  --json
```

Result: `ScaleResults\scale-benchmark.json`. Regression возвращает exit code `10`.

## Legacy Bridge

```powershell
sysdiff legacy matrix --json
sysdiff legacy status comparison .\report-old.json
sysdiff legacy plan comparison .\report-old.json --json
sysdiff legacy verify comparison .\report-v1.json
sysdiff legacy convert comparison .\report-old.json --yes
sysdiff legacy convert bundle .\investigation-old.zip --yes --json
```

Aliases: `upgrade`, `bridge`. Без `--output` создаётся side-by-side `.schema-v1` файл. Existing output требует `--overwrite`.

## Schema Contract Center

```powershell
sysdiff schema list
sysdiff schema list --json
sysdiff schema matrix
sysdiff schema show snapshot
sysdiff schema show comparison
sysdiff schema show bundle
sysdiff schema validate snapshot .\snapshot.json
sysdiff schema validate comparison .\report.json --json
sysdiff schema validate bundle .\manifest.json --json
```

Статусы: `Valid`, `Invalid`, `RequiresNewerSysDiff`. Invalid/future schema возвращает `4`.

## Migration Lab

```powershell
sysdiff migration status
sysdiff migration plan --json
sysdiff migration history
sysdiff migration apply --yes --json
```

Apply создаёт SQLite-consistent backup и выполняет migration в transaction. Ошибка возвращает `9`.

## Compatibility Center

```powershell
sysdiff compatibility status --json
sysdiff compatibility matrix
sysdiff compatibility inspect .\before.sdshot
sysdiff compatibility verify .\before.sdshot
```

Inspection read-only: ZIP paths, required entries, SHA-256, format, Snapshot ID и schema version.

## Обновления

```powershell
sysdiff update check
sysdiff update status --json
sysdiff update download
sysdiff update install --yes --restart
sysdiff update settings --auto-check true --interval-hours 24
sysdiff update settings --auto-download false
sysdiff update settings --ignore 0.12.0
sysdiff update settings --ignore none
sysdiff update clear-cache
```

## Baseline, drift, timeline и cases

```powershell
sysdiff baseline show
sysdiff baseline set <snapshot-name-or-id> --note "trusted"
sysdiff baseline clear
sysdiff drift scan --profile standard --noise Balanced
sysdiff timeline list --limit 100
sysdiff case create "Installer audit" --tags installer,test
sysdiff case list
sysdiff case use <name-or-id>
sysdiff case close <name-or-id>
```

## Снимки и обычное сравнение

```powershell
sysdiff snapshot create before --profile standard
sysdiff snapshot list
sysdiff snapshot show <name-or-id>
sysdiff snapshot delete <name-or-id> --yes
sysdiff snapshot export <name-or-id> --output .\before.sdshot
sysdiff snapshot import .\before.sdshot

sysdiff compare before after --noise Raw
sysdiff compare before after --severity High
sysdiff compare before after --format html --output .\report.html
sysdiff compare before after --format json --output .\report.json
```

Обычный `compare` сохраняет существующие severity/noise/move heuristics, но materialize snapshot. Для bounded-memory файлов используйте `scale compare`.

## Watch, live и bundles

```powershell
sysdiff watch .\Setup.exe --wait-for-children --timeout 900
sysdiff watch --no-launch
sysdiff live process --duration 60
sysdiff live network --duration 60 --format json
sysdiff bundle create <comparison-id> --output .\investigation.zip
```

## Профили, плагины и config

```powershell
sysdiff profile list
sysdiff profile show standard
sysdiff profile load .\profile.json
sysdiff snapshot create plugin-test --plugin C:\Plugins\Example.dll
sysdiff config show
sysdiff config path
```

## Коды завершения

| Код | Значение |
|---:|---|
| 0 | успех / current / plan available |
| 1 | общая, сетевая или update-ошибка |
| 2 | некорректные аргументы или отсутствует confirmation |
| 3 | snapshot/baseline не найдены |
| 4 | legacy/schema/portable incompatibility |
| 5 | доступ запрещён |
| 7 | partial snapshot или безопасный timeout |
| 8 | отменено |
| 9 | storage, future DB или migration error |
| 10 | Scale Lab memory/throughput/count regression |
