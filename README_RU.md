<div align="center">

<img src="assets/logo.svg" alt="SysDiff" width="720">

# 🇷🇺 Русская документация SysDiff

**Основной [`README.md`](README.md) полностью написан на русском и описывает актуальную версию 0.9.0.**

</div>

## 🗃️ Главное изменение 0.9.0

SysDiff получил **Migration Lab** для безопасных изменений локальной SQLite-базы:

- read-only status и dry-run plan;
- явное применение только после `--yes`;
- SQLite-consistent backup;
- transaction rollback;
- история migrations и запусков;
- `PRAGMA user_version` guard;
- отказ от базы из более новой версии;
- JSON-вывод для CI.

```powershell
sysdiff migration status
sysdiff migration plan
sysdiff migration history
sysdiff migration apply --yes
```

Существующая база не мигрируется при обычном запуске. Подробнее: [docs/MIGRATIONS.md](docs/MIGRATIONS.md).

## 🧩 Compatibility Center

```powershell
sysdiff compatibility status
sysdiff compatibility inspect .\before.sdshot
sysdiff compatibility inspect .\before.sdshot --json
```

Inspection проверяет `.sdshot` без записи в SQLite и не импортирует более новую схему частично.

## 🔄 Release Channel

```powershell
sysdiff update check
sysdiff update download
sysdiff update install --yes --restart
```

По умолчанию auto-check включён, auto-download выключен, а установка всегда требует подтверждения.

## 🧭 Drift Operations

```powershell
sysdiff baseline set trusted-clean
sysdiff drift scan --profile standard --noise Balanced
sysdiff timeline list
sysdiff case create "Installer audit" --tags installer,test
```

Интерактивный запуск:

```powershell
sysdiff
```

Безопасный режим интерфейса:

```powershell
$env:SYSDIFF_NO_ANIMATIONS = "1"
$env:NO_COLOR = "1"
sysdiff
```

## 📚 Разделы

- [Главная страница](README.md)
- [Migration Lab](docs/MIGRATIONS.md)
- [Совместимость](docs/COMPATIBILITY.md)
- [Обновления и Release Channel](docs/UPDATES.md)
- [Drift Operations](docs/DRIFT_OPERATIONS.md)
- [Cyber Console](docs/TERMINAL_UI.md)
- [Команды](docs/COMMANDS.md)
- [Архитектура](docs/ARCHITECTURE.md)
- [Провайдеры](docs/PROVIDERS.md)
- [Переносимые форматы](docs/PORTABLE_FORMATS.md)
- [Provider SDK](docs/PROVIDER_SDK.md)
- [Конфиденциальность](docs/PRIVACY.md)
- [Безопасность](docs/SECURITY.md)
- [Решение проблем](docs/TROUBLESHOOTING.md)
- [Roadmap](docs/ROADMAP.md)
- [История изменений](CHANGELOG.md)

> [!IMPORTANT]
> SysDiff не является антивирусом. Версия 0.9.0 пока не имеет Authenticode-подписи; это явно указано в release manifest через `unsigned: true`.
