<div align="center">

<img src="assets/logo.svg" alt="SysDiff" width="720">

# 🇷🇺 Русская документация SysDiff

**Основной [`README.md`](README.md) полностью написан на русском и описывает актуальную версию 0.5.0.**

</div>

## 🟢 Главное изменение 0.5.0

SysDiff получил новый **Cyber Control Node**, визуально приближенный к NexRoute:

```powershell
sysdiff
```

Добавлены нумерованный Command Deck, neon green/cyan theme, boot sequence, Action Console, живой Provider Stream, progress/scanner-анимации и быстрые клавиши `1–9`, `P`, `C`, `W`, `L`, `D`.

Основные модули теперь называются `Snapshot Node`, `Diff Lab`, `Watch Operations`, `Live Signal`, `Report Vault` и `Node Diagnostics`.

Безопасный режим:

```powershell
$env:SYSDIFF_NO_ANIMATIONS = "1"
$env:NO_COLOR = "1"
sysdiff
```

Обычные CLI-команды сохранены для скриптов и CI.

## 📚 Разделы

- [Главная страница](README.md)
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
> SysDiff не является антивирусом и не выносит вердикт о вредоносности.
