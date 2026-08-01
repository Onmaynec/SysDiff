<div align="center">

<img src="assets/logo.svg" alt="SysDiff — расследование изменений Windows" width="720">

# SysDiff

**Cyber-terminal утилита для снимков, сравнения, расследования дрейфа и безопасной проверки переносимых данных Windows.**

[![Сборка](https://github.com/Onmaynec/SysDiff/actions/workflows/build.yml/badge.svg)](https://github.com/Onmaynec/SysDiff/actions/workflows/build.yml)
[![Тесты](https://github.com/Onmaynec/SysDiff/actions/workflows/test.yml/badge.svg)](https://github.com/Onmaynec/SysDiff/actions/workflows/test.yml)
[![Релиз](https://img.shields.io/github/v/release/Onmaynec/SysDiff?display_name=tag&sort=semver)](https://github.com/Onmaynec/SysDiff/releases)
[![Лицензия](https://img.shields.io/badge/license-MIT-40d9d0.svg)](LICENSE)
[![Платформа](https://img.shields.io/badge/Windows-10%20%7C%2011-52a8ff.svg)](#-системные-требования)
[![Версия](https://img.shields.io/badge/version-0.8.0-22c55e.svg)](CHANGELOG.md)

</div>

> [!IMPORTANT]
> **SysDiff не является антивирусом.** Он фиксирует, сравнивает и объясняет системные изменения, но не объявляет объект безопасным или вредоносным.

## 🧩 SysDiff 0.8.0 — Compatibility Center

Версия 0.8.0 добавляет проверяемую совместимость переносимых `.sdshot` до импорта:

- единая матрица container format и snapshot schema;
- read-only inspection без записи в SQLite;
- статусы `Compatible`, `RequiresNewerSysDiff`, `UnsupportedLegacy`, `Invalid`;
- машинный JSON-вывод для CI;
- отказ от частичного импорта newer schema;
- проверка всех ZIP paths и дубликатов entries;
- точная проверка SHA-256;
- согласованность manifest, Snapshot ID и schema version.

```powershell
sysdiff compatibility status
sysdiff compatibility status --json
sysdiff compatibility inspect .\before.sdshot
sysdiff compatibility inspect .\before.sdshot --json
```

Короткий alias:

```powershell
sysdiff compat matrix
sysdiff compat verify .\before.sdshot
```

Подробнее: [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md).

## 🚀 Установка

Откройте GitHub Releases и скачайте:

```text
SysDiff-0.8.0-win-x64.zip
SysDiff-0.8.0-win-x64.zip.sha256
release-manifest.json
```

Распакуйте ZIP и запустите:

```powershell
.\sysdiff.exe
```

Portable package является self-contained и не требует установленного .NET Runtime.

## 🔄 Stable Release Channel

```powershell
sysdiff update check
sysdiff update status
sysdiff update download
sysdiff update install --yes --restart
```

Настройки:

```powershell
sysdiff update settings
sysdiff update settings --auto-check true --interval-hours 24
sysdiff update settings --auto-download true
sysdiff update settings --auto-check false
```

По умолчанию auto-check включён, auto-download выключен, auto-install отсутствует. Установка всегда требует явного подтверждения.

Updater проверяет официальный HTTPS-путь, размер, SHA-256, staged EXE version и выполняет backup/rollback при неуспешной post-install verification.

Подробнее: [docs/UPDATES.md](docs/UPDATES.md).

## 🖥️ Cyber Console

```powershell
sysdiff
```

Из исходников:

```powershell
dotnet run --project .\src\SysDiff.Cli
```

Основные модули:

```text
[01] Snapshot Node
[02] Diff Lab
[03] Drift Operations
[04] Investigation Timeline
[05] Case Vault
[06] Watch Operations
[07] Live Signal
[08] Report Vault
[09] System Node
       └─ Update Center
```

Навигация: `1`…`9`, `↑/↓`, `Enter`, `Esc`, `F5`, `Q`. Для безопасного статического режима:

```powershell
$env:SYSDIFF_NO_ANIMATIONS = "1"
$env:NO_COLOR = "1"
sysdiff
```

## 📸 Снимки и сравнение

```powershell
sysdiff snapshot create before --profile standard

# Установите или запустите исследуемую программу

sysdiff snapshot create after --profile standard
sysdiff compare before after --format html --output .\report.html
```

SysDiff собирает файлы, реестр, службы, Scheduled Tasks, автозагрузку, environment/PATH, Windows Firewall, установленные приложения, драйверы, сертификаты и network configuration.

## 📡 Drift Operations

```powershell
sysdiff baseline set trusted-clean --note "После чистой установки"
sysdiff drift scan --profile standard --noise Balanced
sysdiff timeline list --limit 100
sysdiff case create "Installer audit" --tags installer,test
sysdiff case use "Installer audit"
```

Drift Scan создаёт current snapshot, comparison, explainable risk score `0–100`, HTML/JSON reports и links в active case.

> Drift Risk Score — детерминированная эвристика приоритета анализа, а не вероятность заражения.

## 👀 Watch и Live Monitor

```powershell
sysdiff watch .\Setup.exe --wait-for-children --timeout 900
sysdiff live process --duration 60
sysdiff live network --duration 60
```

Live Monitor не внедряется в процессы, не завершает их, не читает содержимое сетевого трафика и не меняет firewall/network configuration.

## 📦 Переносимые расследования

```powershell
sysdiff snapshot export before --output .\before.sdshot
sysdiff compatibility inspect .\before.sdshot
sysdiff snapshot import .\before.sdshot
```

`.sdshot` содержит manifest, snapshot JSON и SHA-256. Investigation bundle объединяет снимки и готовые отчёты. Неизвестная более новая schema не импортируется частично.

Подробнее: [docs/PORTABLE_FORMATS.md](docs/PORTABLE_FORMATS.md).

## 🧩 Provider SDK

Внешние providers загружаются только явно:

```powershell
sysdiff --plugin .\MyProvider.dll snapshot create custom
```

SDK проверяет совместимую версию контракта. Автоматического поиска DLL и выполнения найденных системных строк нет.

Подробнее: [docs/PROVIDER_SDK.md](docs/PROVIDER_SDK.md).

## 🛠️ Сборка

### Системные требования

- Windows 10 x64 или Windows 11 x64;
- CMD, Windows PowerShell, PowerShell 7 или Windows Terminal;
- .NET 8 SDK для сборки;
- права администратора рекомендуются для полного снимка.

```powershell
git clone https://github.com/Onmaynec/SysDiff.git
cd SysDiff

dotnet restore SysDiff.sln
dotnet build SysDiff.sln --configuration Release
dotnet test SysDiff.sln --configuration Release
```

Portable package:

```powershell
.\scripts\package.ps1 -Version 0.8.0
.\scripts\smoke-test.ps1 -ExpectedVersion 0.8.0
```

Результат:

```text
artifacts\SysDiff-0.8.0-win-x64.zip
artifacts\SysDiff-0.8.0-win-x64.zip.sha256
artifacts\release-manifest.json
```

## 🏷️ Release pipeline

Release PR использует ветку `agent/sysdiff-vX.Y.Z`. После Ready for review workflow ждёт squash merge и затем:

1. сверяет branch/version;
2. запускает полный test suite;
3. собирает self-contained package;
4. выполняет smoke-test;
5. валидирует manifest и SHA-256;
6. создаёт аннотированный tag;
7. публикует provenance attestations;
8. создаёт latest GitHub Release;
9. проверяет release assets.

Повторный запуск существующего tag является безопасным no-op.

## 🔐 Безопасность

- данные хранятся локально;
- providers выполняют только заранее определённое чтение;
- notes, tags, paths и captured commands считаются данными;
- приватные ключи сертификатов не читаются;
- плагины загружаются только через явный `--plugin`;
- `.sdshot` защищён от path traversal, duplicate entries, excessive size и checksum tampering;
- inspection не изменяет SQLite, baseline или active case;
- updater ограничивает host/path, проверяет hash и восстанавливает backup;
- опасные действия требуют подтверждения;
- `Ctrl+C` корректно отменяет длительные операции.

Версия 0.8.0 пока не имеет Authenticode code-signing сертификата. Release manifest честно содержит `unsigned=true`; SHA-256 и GitHub provenance подтверждают целостность и происхождение pipeline, но не заменяют Authenticode.

## 📚 Документация

- [Совместимость и schema policy](docs/COMPATIBILITY.md)
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

## ⚠️ Ограничения

- очень короткоживущие события могут завершиться между интервалами опроса;
- защищённые области требуют администратора;
- большие профили могут содержать сотни тысяч объектов;
- risk score зависит от полноты providers;
- текущая `.sdshot` schema `1` ещё не объявлена стабильной публичной schema 1.0;
- automatic migration неизвестных snapshots не выполняется;
- self-update доступен только опубликованному `sysdiff.exe`, не `dotnet run`;
- SysDiff не заменяет EDR, антивирус или ручную экспертизу.

## 📜 Лицензия

Проект распространяется по лицензии [MIT](LICENSE).

---

<div align="center">

**SysDiff 0.8.0 — inspect compatibility before import.**

</div>
