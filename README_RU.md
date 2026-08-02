<div align="center">

<img src="assets/logo.svg" alt="SysDiff" width="720">

# 🇷🇺 Русская документация SysDiff

**Основной [`README.md`](README.md) описывает актуальную версию 0.12.0.**

</div>

## 🚀 Главное изменение 0.12.0

Scale Lab добавляет bounded-memory обработку больших NDJSON datasets:

```powershell
sysdiff scale synth .\before.ndjson --count 1000000 --variant before
sysdiff scale synth .\after.ndjson --count 1000000 --variant after --change-every 1000
sysdiff scale sort .\unsorted.ndjson --output .\sorted.ndjson
sysdiff scale compare .\before.ndjson .\after.ndjson --output .\changes.ndjson
sysdiff scale benchmark --output-dir .\ScaleResults --artifacts 1000000 --json
```

CI выполняет benchmark на 1 000 000 artifacts и блокирует memory/throughput/count regressions. Подробнее: [docs/SCALE_LAB.md](docs/SCALE_LAB.md).

## 🌉 Legacy Bridge

```powershell
sysdiff legacy plan comparison .\report-old.json
sysdiff legacy convert comparison .\report-old.json --yes
sysdiff legacy verify comparison .\report-old.schema-v1.json
```

Portable data 0.3–0.9 преобразуются с backup, SHA-256 audit и atomic output.

## 📐 Schema, migration и compatibility

```powershell
sysdiff schema list
sysdiff schema validate comparison .\report.json --json
sysdiff migration status
sysdiff migration plan
sysdiff migration apply --yes
sysdiff compatibility inspect .\before.sdshot
```

Public Schema Contract остаётся v1. SQLite migration и portable conversion не выполняются автоматически.

## 🔄 Release Channel

```powershell
sysdiff update check
sysdiff update download
sysdiff update install --yes --restart
```

## 📚 Разделы

- [Главная страница](README.md)
- [Scale Lab](docs/SCALE_LAB.md)
- [Legacy Bridge](docs/LEGACY_BRIDGE.md)
- [Schema Contract v1](docs/SCHEMA_CONTRACT.md)
- [Совместимость](docs/COMPATIBILITY.md)
- [Migration Lab](docs/MIGRATIONS.md)
- [Команды](docs/COMMANDS.md)
- [Архитектура](docs/ARCHITECTURE.md)
- [Roadmap](docs/ROADMAP.md)
- [История изменений](CHANGELOG.md)

> [!IMPORTANT]
> SysDiff не является антивирусом. Версия 0.12.0 пока не имеет Authenticode-подписи; release manifest содержит `unsigned: true`.
