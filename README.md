<div align="center">

# SysDiff

**Cyber-terminal утилита для снимков Windows, сравнения, расследования дрейфа и безопасного управления переносимыми данными.**

[![Сборка](https://github.com/Onmaynec/SysDiff/actions/workflows/build.yml/badge.svg)](https://github.com/Onmaynec/SysDiff/actions/workflows/build.yml)
[![Тесты](https://github.com/Onmaynec/SysDiff/actions/workflows/test.yml/badge.svg)](https://github.com/Onmaynec/SysDiff/actions/workflows/test.yml)
[![Релиз](https://img.shields.io/github/v/release/Onmaynec/SysDiff?display_name=tag&sort=semver)](https://github.com/Onmaynec/SysDiff/releases)
[![Лицензия](https://img.shields.io/badge/license-MIT-40d9d0.svg)](LICENSE)
[![Версия](https://img.shields.io/badge/version-0.10.0-22c55e.svg)](CHANGELOG.md)

</div>

> [!IMPORTANT]
> **SysDiff не является антивирусом.** Он фиксирует и объясняет системные изменения, но не объявляет объект безопасным или вредоносным.

## 📐 SysDiff 0.10.0 — Schema Contract

Версия 0.10.0 публикует стабильный публичный **Schema Contract v1**:

- JSON Schema Draft 2020-12 для snapshot, comparison report и bundle manifest;
- embedded schemas внутри приложения;
- golden fixtures в CI и portable package;
- обязательные поля, UUID, RFC3339, SemVer, enum и range validation;
- unknown additive properties разрешены;
- future schema получает `RequiresNewerSysDiff`;
- breaking change требует schema major 2 и migration guide.

```powershell
sysdiff schema list
sysdiff schema list --json
sysdiff schema show snapshot
sysdiff schema validate snapshot .\snapshot.json
sysdiff schema validate comparison .\report.json --json
sysdiff schema validate bundle .\manifest.json --json
```

Подробнее: [docs/SCHEMA_CONTRACT.md](docs/SCHEMA_CONTRACT.md).

## Reader/writer matrix

| Формат | Writer 0.10 | Reader 0.10 | Policy |
|---|---:|---:|---|
| `.sdshot` snapshot JSON | schema 1 | schema 1 | additive fields allowed |
| comparison JSON report | schema 1 | schema 1 | breaking change → major 2 |
| investigation bundle manifest | schema 1 | schema 1 | future schema rejected |
| SQLite database | user_version 9 | `0..9` | migration через backup/transaction |

Snapshot contract сохраняет исторический PascalCase `.sdshot`. Comparison report и bundle manifest используют camelCase.

## 🗃️ Migration Lab

```powershell
sysdiff migration status
sysdiff migration plan
sysdiff migration history
sysdiff migration apply --yes
```

`plan` read-only. Existing database изменяется только после `--yes`; перед SQL создаётся и проверяется SQLite-consistent backup. Ошибка откатывает transaction.

Подробнее: [docs/MIGRATIONS.md](docs/MIGRATIONS.md).

## 🧩 Compatibility Center

```powershell
sysdiff compatibility status
sysdiff compatibility inspect .\before.sdshot
sysdiff compatibility inspect .\before.sdshot --json
```

Inspection проверяет ZIP paths, manifest, Snapshot ID, schema и SHA-256 без записи в SQLite.

Подробнее: [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md).

## 🚀 Установка

Скачайте из GitHub Releases:

```text
SysDiff-0.10.0-win-x64.zip
SysDiff-0.10.0-win-x64.zip.sha256
release-manifest.json
```

Распакуйте и запустите:

```powershell
.\sysdiff.exe
```

Portable package self-contained и не требует установленного .NET Runtime. Внутри также находятся:

```text
SCHEMA_CONTRACT.txt
schemas\public\v1\*.schema.json
schema-fixtures\v1\*.json
MIGRATIONS.txt
COMPATIBILITY.txt
UPDATES.txt
```

## 🖥️ Основные workflows

### Snapshot и comparison

```powershell
sysdiff snapshot create before --profile standard
# Запустите исследуемое приложение
sysdiff snapshot create after --profile standard
sysdiff compare before after --format html --output .\report.html
sysdiff compare before after --format json --output .\report.json
```

### Drift Operations

```powershell
sysdiff baseline set trusted-clean --note "После чистой установки"
sysdiff drift scan --profile standard --noise Balanced
sysdiff timeline list --limit 100
sysdiff case create "Installer audit" --tags installer,test
```

### Watch и Live Monitor

```powershell
sysdiff watch .\Setup.exe --wait-for-children --timeout 900
sysdiff live process --duration 60
sysdiff live network --duration 60
```

### Portable investigation

```powershell
sysdiff snapshot export before --output .\before.sdshot
sysdiff compatibility inspect .\before.sdshot
sysdiff snapshot import .\before.sdshot
sysdiff bundle create <comparison-id> --output .\investigation.zip
```

## 🔄 Stable Release Channel

```powershell
sysdiff update check
sysdiff update download
sysdiff update install --yes --restart
```

Updater проверяет официальный HTTPS-path, размер, SHA-256 и staged executable version. Установка выполняет backup и rollback при failed verification.

## 🛠️ Сборка

Требования: Windows 10/11 x64 и .NET 8 SDK.

```powershell
git clone https://github.com/Onmaynec/SysDiff.git
cd SysDiff
dotnet restore SysDiff.sln
dotnet build SysDiff.sln --configuration Release
dotnet test SysDiff.sln --configuration Release
.\scripts\package.ps1 -Version 0.10.0
.\scripts\smoke-test.ps1 -ExpectedVersion 0.10.0
```

## 🔐 Безопасность

- schemas и validation read-only;
- unknown properties не выполняются как код;
- future schema не интерпретируется частично;
- migration требует подтверждения и backup;
- `.sdshot` защищён от traversal, duplicate entries и checksum tampering;
- plugins загружаются только через явный `--plugin`;
- updater не заменяет работающий EXE напрямую;
- данные остаются локальными.

Версия 0.10.0 пока публикуется без Authenticode. Manifest честно содержит `unsigned=true`; SHA-256 и GitHub provenance подтверждают целостность pipeline, но не заменяют code-signing сертификат.

## 📚 Документация

- [Schema Contract v1](docs/SCHEMA_CONTRACT.md)
- [Compatibility matrix](docs/COMPATIBILITY.md)
- [Migration Lab](docs/MIGRATIONS.md)
- [Обновления](docs/UPDATES.md)
- [Команды](docs/COMMANDS.md)
- [Архитектура](docs/ARCHITECTURE.md)
- [Переносимые форматы](docs/PORTABLE_FORMATS.md)
- [Provider SDK](docs/PROVIDER_SDK.md)
- [Безопасность](docs/SECURITY.md)
- [Roadmap](docs/ROADMAP.md)
- [История изменений](CHANGELOG.md)

## ⚠️ Ограничения

- stable Schema Contract v1 не означает завершение всех требований продукта 1.0;
- reports/bundles, созданные до 0.10, считаются pre-contract;
- automatic repair повреждённых JSON отсутствует;
- Authenticode ещё не настроен;
- SysDiff не заменяет EDR, антивирус или ручную экспертизу.

<div align="center">

**SysDiff 0.10.0 — validate the contract before trusting the data.**

</div>
