<div align="center">

<img src="assets/logo.svg" alt="SysDiff" width="720">

# 🇷🇺 Русская документация SysDiff

**Основной [`README.md`](README.md) описывает актуальную версию 0.10.0.**

</div>

## 📐 Главное изменение 0.10.0

SysDiff получил стабильный **Schema Contract v1**:

- JSON Schema Draft 2020-12;
- snapshot, comparison report и bundle manifest;
- CLI validation и embedded schemas;
- golden fixtures в CI;
- unknown additive fields разрешены;
- future schema блокируется;
- breaking change требует нового schema major.

```powershell
sysdiff schema list
sysdiff schema show snapshot
sysdiff schema validate snapshot .\snapshot.json
sysdiff schema validate comparison .\report.json --json
sysdiff schema validate bundle .\manifest.json --json
```

Подробнее: [docs/SCHEMA_CONTRACT.md](docs/SCHEMA_CONTRACT.md).

## 🗃️ Migration Lab

```powershell
sysdiff migration status
sysdiff migration plan
sysdiff migration history
sysdiff migration apply --yes
```

Существующая база не мигрируется при обычном запуске. Apply создаёт backup и выполняется транзакционно.

## 🧩 Compatibility Center

```powershell
sysdiff compatibility status
sysdiff compatibility inspect .\before.sdshot
```

Inspection проверяет `.sdshot` без записи в SQLite и не импортирует более новую schema частично.

## 🔄 Release Channel

```powershell
sysdiff update check
sysdiff update download
sysdiff update install --yes --restart
```

## 📚 Разделы

- [Главная страница](README.md)
- [Schema Contract v1](docs/SCHEMA_CONTRACT.md)
- [Совместимость](docs/COMPATIBILITY.md)
- [Migration Lab](docs/MIGRATIONS.md)
- [Обновления](docs/UPDATES.md)
- [Команды](docs/COMMANDS.md)
- [Архитектура](docs/ARCHITECTURE.md)
- [Переносимые форматы](docs/PORTABLE_FORMATS.md)
- [Provider SDK](docs/PROVIDER_SDK.md)
- [Безопасность](docs/SECURITY.md)
- [Roadmap](docs/ROADMAP.md)
- [История изменений](CHANGELOG.md)

> [!IMPORTANT]
> SysDiff не является антивирусом. Версия 0.10.0 пока не имеет Authenticode-подписи; release manifest содержит `unsigned: true`.
