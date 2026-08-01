<div align="center">

<img src="assets/logo.svg" alt="SysDiff — Узнай, что изменилось в Windows" width="720">

# SysDiff

**Консольный инструмент для снимков, сравнения и локального расследования изменений Windows.**

[![Сборка](https://github.com/Onmaynec/SysDiff/actions/workflows/build.yml/badge.svg)](https://github.com/Onmaynec/SysDiff/actions/workflows/build.yml)
[![Тесты](https://github.com/Onmaynec/SysDiff/actions/workflows/test.yml/badge.svg)](https://github.com/Onmaynec/SysDiff/actions/workflows/test.yml)
[![Релиз](https://img.shields.io/github/v/release/Onmaynec/SysDiff?display_name=tag&sort=semver)](https://github.com/Onmaynec/SysDiff/releases)
[![Лицензия](https://img.shields.io/badge/license-MIT-40d9d0.svg)](LICENSE)
[![Платформа](https://img.shields.io/badge/Windows-10%20%7C%2011-52a8ff.svg)](#-системные-требования)

</div>

> [!IMPORTANT]
> **SysDiff не является антивирусом.** Он фиксирует и объясняет системные изменения, но не объявляет объект безопасным или вредоносным.

<img src="assets/screenshots/overview.svg" alt="Пример отчёта SysDiff">

## 🔍 Что делает SysDiff

SysDiff создаёт снимки Windows до и после запуска программы, сравнивает их по стабильным идентификаторам и формирует Console, JSON, Markdown или автономный HTML-отчёт.

```powershell
sysdiff snapshot create before --profile standard

# Установите или запустите исследуемую программу

sysdiff snapshot create after --profile standard
sysdiff compare before after --format html --output .\report.html
```

Версия **0.3.0** дополняет классические снимки инструментами расследования: live process/network monitor, переносимым `.sdshot`, investigation bundle, межмашинным сравнением, пользовательскими профилями и Provider SDK.

## ✨ Возможности 0.3.0

- 📸 снимки файлов, реестра, служб, задач, автозагрузки и окружения;
- 🧱 Windows Firewall, установленные приложения, драйверы и сертификаты;
- 🌐 адаптеры, DNS, шлюзы, proxy и маршруты;
- 🔴 live-события запуска и завершения процессов;
- 📡 live-события открытия и закрытия TCP/UDP endpoints;
- ↔️ определение уникальных перемещений и переименований файлов;
- 🖥️ явное межмашинное сравнение с предупреждениями и confidence;
- 📦 экспорт и импорт снимков `.sdshot` с SHA-256;
- 🧳 investigation bundle со снимками и отчётами;
- 🎛️ пользовательские JSON-профили;
- 🧩 Provider SDK и явная загрузка внешних плагинов;
- 🕶️ автоматическое маскирование `%USERPROFILE%`;
- 🧹 фильтрация шума `Raw`, `Balanced`, `Strict`;
- 🗃️ SQLite, portable mode, self-contained `win-x64`;
- ⚙️ GitHub Actions, unit-тесты и smoke-тесты.

## 🚀 Быстрый старт

### Системные требования

- Windows 10 x64 или Windows 11 x64;
- для сборки — .NET 8 SDK;
- Windows PowerShell 5.1 или PowerShell 7;
- права администратора рекомендуются для полного системного снимка.

```powershell
git clone https://github.com/Onmaynec/SysDiff.git
cd SysDiff

dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

Portable-пакет:

```powershell
.\scripts\package.ps1
```

```text
SysDiff-0.3.0-win-x64.zip
SysDiff-0.3.0-win-x64.zip.sha256
```

## 🧭 Команды 0.3

### Снимки и переносимый формат

```powershell
sysdiff snapshot create before
sysdiff snapshot create custom --profile-file .\profile.json
sysdiff snapshot export before --output .\before.sdshot
sysdiff snapshot import .\before.sdshot
```

`.sdshot` содержит `manifest.json`, `snapshot.json` и `checksums.sha256`. Импорт проверяет схему, размер, структуру архива и SHA-256.

### Сравнение

```powershell
sysdiff compare before after
sysdiff compare before after --noise Strict --severity Medium
sysdiff compare pc-a pc-b --cross-machine --format html --output .\cross-machine.html
```

При межмашинном сравнении SysDiff показывает различия Windows build и архитектуры, снижает confidence и требует явный флаг `--cross-machine`.

### Live process monitor

```powershell
sysdiff live process --duration 60 --format json
sysdiff live process --duration 120 --root-pid 1234 --format markdown
```

Режим фиксирует обнаруженные события `Started` и `Stopped`. Он не внедряется в процессы, не приостанавливает и не завершает их.

### Live network monitor

```powershell
sysdiff live network --duration 60 --format json
```

Фиксируются изменения TCP-соединений и UDP listeners. Содержимое сетевого трафика не перехватывается.

### Investigation bundle

```powershell
sysdiff bundle create <comparison-id> --output .\investigation.zip
```

Bundle включает два `.sdshot`, HTML/JSON/Markdown-отчёты, manifest и SHA-256. Сырые логи и приватные ключи не добавляются.

### Пользовательские профили

```powershell
sysdiff profile load .\samples\profiles\installer-audit.json
sysdiff snapshot create before --profile-file .\samples\profiles\installer-audit.json
```

Неизвестные провайдеры и небезопасные лимиты отклоняются до начала снимка.

### Provider SDK

```powershell
dotnet build .\samples\plugins\SysDiff.SampleProvider\SysDiff.SampleProvider.csproj
sysdiff snapshot create plugin-shot --profile-file .\plugin-profile.json `
  --plugin .\SysDiff.SampleProvider.dll
```

Плагины **никогда не загружаются автоматически**. Пользователь обязан явно передать точный путь через `--plugin`.

Полный справочник: [docs/COMMANDS.md](docs/COMMANDS.md).

## 🧩 Провайдеры

| ID | Данные | Версия |
|---|---|---:|
| `filesystem` | файлы, каталоги, метаданные, SHA-256 | 0.1 |
| `registry` | HKCU/HKLM/HKCR, x86/x64, redaction | 0.1 |
| `services` | службы, запуск, аккаунт, зависимости | 0.1 |
| `scheduled-tasks` | задачи, действия и триггеры | 0.1 |
| `startup` | Run/RunOnce, Startup Folder, Winlogon | 0.1 |
| `environment` | переменные и элементы PATH | 0.1 |
| `firewall` | правила, порты, адреса, программы | 0.2 |
| `installed-apps` | приложения user/machine, x86/x64 | 0.2 |
| `drivers` | пути, состояния, SHA-256, подписи | 0.2 |
| `certificates` | хранилища, сроки и доверие | 0.2 |
| `network-configuration` | adapters, DNS, gateways, proxy, routes | 0.3 |

Подробнее: [docs/PROVIDERS.md](docs/PROVIDERS.md).

## 🔐 Безопасность и конфиденциальность

- системные пути, аргументы и команды рассматриваются только как данные;
- значения реестра с признаками секрета заменяются на `<redacted>`;
- пути `C:\Users\<имя>` автоматически заменяются на `%USERPROFILE%`;
- machine fingerprint хранится как SHA-256, а не открытое имя компьютера;
- приватные ключи сертификатов не извлекаются;
- live network monitor не читает пакеты;
- `.sdshot` проверяет ZIP Slip, размеры и checksums;
- плагины являются исполняемым кодом и загружаются только явно;
- ошибка одного провайдера приводит к `Partial`, но не уничтожает снимок.

Подробнее: [docs/PRIVACY.md](docs/PRIVACY.md), [docs/SECURITY.md](docs/SECURITY.md).

## ⚠️ Ограничения

- polling может пропустить очень короткоживущий процесс или endpoint;
- live network monitor версии 0.3 не гарантирует сопоставление endpoint с PID;
- move/rename определяется только при уникальном совпадении SHA-256 и размера;
- неоднозначные совпадения остаются `Added`/`Removed`;
- cross-machine confidence не заменяет ручной анализ;
- плагины выполняются с правами процесса SysDiff;
- оценка важности не является вердиктом о вредоносности.

## 📚 Документация

- [Команды](docs/COMMANDS.md)
- [Архитектура](docs/ARCHITECTURE.md)
- [Провайдеры](docs/PROVIDERS.md)
- [Live Monitor](docs/LIVE_MONITOR.md)
- [Переносимые форматы](docs/PORTABLE_FORMATS.md)
- [Provider SDK](docs/PROVIDER_SDK.md)
- [Конфиденциальность](docs/PRIVACY.md)
- [Решение проблем](docs/TROUBLESHOOTING.md)
- [Roadmap](docs/ROADMAP.md)

## 📜 Лицензия

Проект распространяется по лицензии [MIT](LICENSE).

---

<div align="center">

**SysDiff — узнай, что изменилось в Windows.** 🪟🔎

</div>
