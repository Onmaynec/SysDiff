<div align="center">

# SysDiff

**Cyber-terminal утилита для снимков Windows, сравнения, расследования дрейфа и безопасной эволюции данных.**

[![Сборка](https://github.com/Onmaynec/SysDiff/actions/workflows/build.yml/badge.svg)](https://github.com/Onmaynec/SysDiff/actions/workflows/build.yml)
[![Тесты](https://github.com/Onmaynec/SysDiff/actions/workflows/test.yml/badge.svg)](https://github.com/Onmaynec/SysDiff/actions/workflows/test.yml)
[![Scale](https://github.com/Onmaynec/SysDiff/actions/workflows/scale.yml/badge.svg)](https://github.com/Onmaynec/SysDiff/actions/workflows/scale.yml)
[![Релиз](https://img.shields.io/github/v/release/Onmaynec/SysDiff?display_name=tag&sort=semver)](https://github.com/Onmaynec/SysDiff/releases)
[![Лицензия](https://img.shields.io/badge/license-MIT-40d9d0.svg)](LICENSE)
[![Версия](https://img.shields.io/badge/version-0.12.0-22c55e.svg)](CHANGELOG.md)

</div>

> [!IMPORTANT]
> **SysDiff не является антивирусом.** Он фиксирует и объясняет изменения, но не объявляет объект безопасным или вредоносным.

## 🚀 SysDiff 0.12.0 — Scale Lab

Scale Lab добавляет bounded-memory workflow для миллионов artifacts:

- SysDiff Artifact NDJSON v1;
- synthetic generator до 10 000 000 записей;
- external chunk sort и k-way merge;
- streaming merge-join comparison;
- NDJSON change report без списка изменений в памяти;
- managed-memory, working-set и throughput telemetry;
- regression gate с exit code `10`;
- отдельный CI benchmark на **1 000 000** artifacts.

```powershell
sysdiff scale synth .\before.ndjson --count 1000000 --variant before
sysdiff scale synth .\after.ndjson --count 1000000 --variant after --change-every 1000
sysdiff scale sort .\unsorted.ndjson --output .\sorted.ndjson --batch-size 50000
sysdiff scale compare .\before.ndjson .\after.ndjson --output .\changes.ndjson
sysdiff scale benchmark --output-dir .\ScaleResults --artifacts 1000000 --json
```

`scale compare` держит только текущую before/after пару. `scale sort` держит один ограниченный batch и cursors chunk-файлов. Подробнее: [docs/SCALE_LAB.md](docs/SCALE_LAB.md).

## 🌉 Legacy Bridge

```powershell
sysdiff legacy plan comparison .\report-old.json
sysdiff legacy convert comparison .\report-old.json --yes
sysdiff legacy plan bundle .\investigation-old.zip
sysdiff legacy convert bundle .\investigation-old.zip --yes
```

Portable formats 0.3–0.9 преобразуются в Schema Contract v1 с backup, SHA-256 audit, atomic output и повторной verification. Вложенные `.sdshot` сохраняются byte-for-byte.

Подробнее: [docs/LEGACY_BRIDGE.md](docs/LEGACY_BRIDGE.md).

## 📐 Schema Contract v1

```powershell
sysdiff schema list
sysdiff schema show snapshot
sysdiff schema validate snapshot .\snapshot.json
sysdiff schema validate comparison .\report.json --json
sysdiff schema validate bundle .\manifest.json --json
```

Public JSON Schema Draft 2020-12 остаётся major `1`. Operational Scale NDJSON является отдельным stream format и не меняет portable contract.

## 🗃️ Migration и Compatibility

```powershell
sysdiff migration status
sysdiff migration plan
sysdiff migration apply --yes

sysdiff compatibility inspect .\before.sdshot
```

SQLite migration требует explicit confirmation, verified backup и transaction. `.sdshot` inspection проверяет ZIP paths, manifest, IDs и checksums до import.

## 🚀 Установка

Скачайте:

```text
SysDiff-0.12.0-win-x64.zip
SysDiff-0.12.0-win-x64.zip.sha256
release-manifest.json
```

Portable package self-contained и включает:

```text
SCALE_LAB.txt
LEGACY_BRIDGE.txt
SCHEMA_CONTRACT.txt
MIGRATIONS.txt
COMPATIBILITY.txt
UPDATES.txt
schemas\public\v1\*.schema.json
schema-fixtures\v1\*.json
legacy-fixtures\v0.9\*.json
```

## Основные workflows

```powershell
sysdiff snapshot create before --profile standard
# Запустите исследуемое приложение
sysdiff snapshot create after --profile standard
sysdiff compare before after --format html --output .\report.html

sysdiff baseline set trusted-clean
sysdiff drift scan --profile standard --noise Balanced
sysdiff timeline list --limit 100

sysdiff watch .\Setup.exe --wait-for-children --timeout 900
sysdiff live process --duration 60
sysdiff live network --duration 60
```

## Обновление

```powershell
sysdiff update check
sysdiff update download
sysdiff update install --yes --restart
```

Updater проверяет официальный release manifest, размер, SHA-256 и staged executable version. Failed verification запускает rollback.

## Сборка

Требования: Windows 10/11 x64 и .NET 8 SDK.

```powershell
git clone https://github.com/Onmaynec/SysDiff.git
cd SysDiff
dotnet restore SysDiff.sln
dotnet build SysDiff.sln --configuration Release
dotnet test SysDiff.sln --configuration Release
.\scripts\package.ps1 -Version 0.12.0
.\scripts\smoke-test.ps1 -ExpectedVersion 0.12.0
```

Million-object benchmark:

```powershell
dotnet run --project .\src\SysDiff.Cli --configuration Release -- `
  scale benchmark --output-dir .\ScaleResults --artifacts 1000000 --json
```

## Безопасность и ограничения

- Scale input не исполняется как код, SQL или команда;
- invalid JSON, missing/duplicate identity и unsorted comparison input отклоняются;
- NDJSON line ограничена 4 MiB;
- sort/compare output записывается атомарно;
- future portable schema не downgrades;
- migration и portable conversion требуют backup/confirmation;
- plugins загружаются только через явный `--plugin`;
- обычная SQLite-команда `compare` пока materialize snapshot; bounded-memory path — отдельный `scale` workflow;
- Authenticode ещё не настроен, manifest содержит `unsigned=true`.

## Документация

- [Scale Lab](docs/SCALE_LAB.md)
- [Legacy Bridge](docs/LEGACY_BRIDGE.md)
- [Schema Contract v1](docs/SCHEMA_CONTRACT.md)
- [Compatibility matrix](docs/COMPATIBILITY.md)
- [Migration Lab](docs/MIGRATIONS.md)
- [Команды](docs/COMMANDS.md)
- [Архитектура](docs/ARCHITECTURE.md)
- [Roadmap](docs/ROADMAP.md)
- [История изменений](CHANGELOG.md)

<div align="center">

**SysDiff 0.12.0 — compare a million records without holding a million records.**

</div>
