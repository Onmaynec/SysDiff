<div align="center">

<img src="assets/logo.svg" alt="SysDiff" width="720">

# 🇷🇺 Русская документация SysDiff

**Основной [`README.md`](README.md) полностью написан на русском и описывает актуальную версию 0.6.0.**

</div>

## 🧭 Главное изменение 0.6.0

SysDiff получил **Drift Operations**:

- доверенную baseline;
- быстрый Drift Scan;
- explainable risk score `0–100`;
- Investigation Timeline;
- Case Vault;
- автоматическую привязку snapshot/comparison/report к активному кейсу;
- additive SQLite migration без изменения старых таблиц.

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

Безопасный режим:

```powershell
$env:SYSDIFF_NO_ANIMATIONS = "1"
$env:NO_COLOR = "1"
sysdiff
```

## 📚 Разделы

- [Главная страница](README.md)
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
> SysDiff не является антивирусом, а Drift Risk Score не является вероятностью заражения.
