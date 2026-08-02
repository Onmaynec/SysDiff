# 🚀 SysDiff Scale Lab 0.12

Scale Lab обрабатывает большие наборы artifacts без загрузки всех объектов и изменений в память.

## Формат потока

Используется **SysDiff Artifact NDJSON v1**: одна JSON-запись `SystemArtifact` на строку.

```json
{"providerId":"filesystem","artifactType":"File","identity":"file://c:/app.exe","displayName":"app.exe","properties":{},"tags":[]}
```

Для `scale compare` входы должны быть отсортированы по `identity` с `OrdinalIgnoreCase` ordering и не содержать duplicate identities.

## Команды

### Синтетический dataset

```powershell
sysdiff scale synth .\before.ndjson --count 1000000 --variant before
sysdiff scale synth .\after.ndjson --count 1000000 --variant after --change-every 1000
```

Generator пишет строки непосредственно в файл и не создаёт million-object collection.

### External sort

```powershell
sysdiff scale sort .\unsorted.ndjson `
  --output .\sorted.ndjson `
  --batch-size 50000
```

Алгоритм:

1. читает ограниченный batch;
2. сортирует batch по identity;
3. записывает temporary chunk;
4. выполняет k-way merge chunks;
5. отклоняет duplicate identities;
6. атомарно публикует output.

### Streaming comparison

```powershell
sysdiff scale compare .\before.ndjson .\after.ndjson `
  --output .\changes.ndjson
```

Merge-join держит только текущие before/after artifacts. Изменения записываются в NDJSON сразу после вычисления. Unchanged records по умолчанию не записываются.

### Benchmark и regression gate

```powershell
sysdiff scale benchmark `
  --output-dir .\ScaleResults `
  --artifacts 1000000 `
  --max-managed-mb 256 `
  --min-throughput 1000 `
  --json
```

Результат сохраняется как `scale-benchmark.json`. Exit code `10` означает регрессию memory, throughput или expected change count.

## Memory model

Scale Lab ограничивает живое состояние:

- generator: один artifact;
- sort: один batch плюс один cursor на chunk;
- comparison: одна before/after пара;
- report: одна change record;
- telemetry: counters и peak values.

Порог CI относится к managed heap. Working set публикуется отдельно, поскольку включает runtime, mapped files и native buffers.

## CI

Workflow `Scale benchmark` выполняет тест на **1 000 000** artifacts и публикует artifact `scale-benchmark-1000000`:

```text
scale-benchmark.json
console.json
scale-changes.ndjson
```

Gate проверяет:

- ровно 1 000 000 artifacts;
- ожидаемые 1 000 modified records;
- managed peak не выше 256 MiB;
- throughput не ниже 1 000 artifacts/sec.

## Безопасность

- максимальное число synthetic artifacts — 10 000 000;
- максимальная NDJSON line — 4 MiB;
- invalid JSON и missing identity отклоняются;
- unsorted input не сравнивается молча;
- duplicate identity блокирует sort/compare;
- output записывается через temporary file и atomic move;
- input files не изменяются;
- NDJSON values не исполняются как команды, SQL или код.

## Ограничения 0.12

- обычные snapshots в SQLite всё ещё materialize при старой команде `compare`;
- Scale Lab работает с отдельным NDJSON stream format;
- move/rename heuristic и noise filtering не применяются к scale stream;
- HTML/Markdown pagination для scale changes запланирована отдельно;
- benchmark numbers зависят от hardware runner и используются только как conservative regression floor.
