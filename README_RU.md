<div align="center">

<img src="assets/logo.svg" alt="SysDiff" width="720">

# 🇷🇺 Русская документация SysDiff

**Основной [`README.md`](README.md) полностью написан на русском и описывает актуальную версию 0.8.0.**

</div>

## 🧩 Главное изменение 0.8.0

SysDiff получил **Compatibility Center** для проверки `.sdshot` до импорта:

- format/schema compatibility matrix;
- read-only inspection без записи в SQLite;
- JSON-вывод для CI;
- статусы compatible/newer/legacy/invalid;
- SHA-256, ZIP path и duplicate entry validation;
- отказ от частичного импорта более новой схемы.

```powershell
sysdiff compatibility status
sysdiff compatibility inspect .\before.sdshot
sysdiff compatibility inspect .\before.sdshot --json
```

Подробнее: [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md).

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
> SysDiff не является антивирусом. Версия 0.8.0 пока не имеет Authenticode-подписи; это явно указано в release manifest через `unsigned: true`.
