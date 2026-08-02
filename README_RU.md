<div align="center">

<img src="assets/logo.svg" alt="SysDiff" width="720">

# 🇷🇺 Русская документация SysDiff

**Основной [`README.md`](README.md) описывает актуальную версию 0.11.0.**

</div>

## 🌉 Главное изменение 0.11.0

SysDiff получил **Legacy Bridge** для безопасного преобразования portable data 0.3–0.9 в Schema Contract v1:

- comparison JSON reports;
- investigation bundle ZIP;
- read-only plan;
- explicit `--yes`;
- automatic backup;
- atomic output;
- SHA-256 audit;
- сохранение вложенных `.sdshot` byte-for-byte;
- post-conversion verify.

```powershell
sysdiff legacy matrix
sysdiff legacy plan comparison .\report-old.json
sysdiff legacy convert comparison .\report-old.json --yes
sysdiff legacy verify comparison .\report-old.schema-v1.json
```

Подробнее: [docs/LEGACY_BRIDGE.md](docs/LEGACY_BRIDGE.md).

## 📐 Schema Contract v1

```powershell
sysdiff schema list
sysdiff schema show snapshot
sysdiff schema validate comparison .\report.json --json
```

Unknown additive fields разрешены, future schema блокируется, breaking change требует нового schema major.

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

Inspection проверяет `.sdshot` без записи в SQLite. Legacy Bridge не переписывает уже совместимые snapshot archives.

## 🔄 Release Channel

```powershell
sysdiff update check
sysdiff update download
sysdiff update install --yes --restart
```

## 📚 Разделы

- [Главная страница](README.md)
- [Legacy Bridge](docs/LEGACY_BRIDGE.md)
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
> SysDiff не является антивирусом. Версия 0.11.0 пока не имеет Authenticode-подписи; release manifest содержит `unsigned: true`.
