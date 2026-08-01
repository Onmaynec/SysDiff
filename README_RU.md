<div align="center">

<img src="assets/logo.svg" alt="SysDiff" width="720">

# 🇷🇺 Русская документация SysDiff

**Основной [`README.md`](README.md) полностью написан на русском и описывает актуальную версию 0.7.0.**

</div>

## 🚀 Главное изменение 0.7.0

SysDiff получил полноценный **Release Channel**:

- аннотированные теги `vX.Y.Z`;
- GitHub Releases с portable ZIP;
- SHA-256 и проверяемый `release-manifest.json`;
- GitHub provenance attestations;
- встроенную проверку stable channel;
- Update Center в Cyber Console;
- безопасный staging, backup, verification и rollback;
- auto-check без автоматической установки.

```powershell
sysdiff update check
sysdiff update download
sysdiff update install --yes --restart
```

По умолчанию auto-check включён, auto-download выключен, а установка всегда требует подтверждения.

## 🧭 Drift Operations сохранены

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

В `System Node` доступен новый `Update Center`.

Безопасный режим интерфейса:

```powershell
$env:SYSDIFF_NO_ANIMATIONS = "1"
$env:NO_COLOR = "1"
sysdiff
```

## 📚 Разделы

- [Главная страница](README.md)
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
> SysDiff не является антивирусом. Версия 0.7.0 пока не имеет Authenticode-подписи; это явно указано в release manifest через `unsigned: true`.
