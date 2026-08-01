<div align="center">

<img src="assets/logo.svg" alt="SysDiff — Узнай, что изменилось в Windows" width="720">

# SysDiff

**Консольный инструмент для сравнения состояния Windows до и после установки или запуска программы.**

[![Сборка](https://github.com/Onmaynec/SysDiff/actions/workflows/build.yml/badge.svg)](https://github.com/Onmaynec/SysDiff/actions/workflows/build.yml)
[![Тесты](https://github.com/Onmaynec/SysDiff/actions/workflows/test.yml/badge.svg)](https://github.com/Onmaynec/SysDiff/actions/workflows/test.yml)
[![Релиз](https://img.shields.io/github/v/release/Onmaynec/SysDiff?display_name=tag&sort=semver)](https://github.com/Onmaynec/SysDiff/releases)
[![Лицензия](https://img.shields.io/badge/license-MIT-40d9d0.svg)](LICENSE)
[![Платформа](https://img.shields.io/badge/Windows-10%20%7C%2011-52a8ff.svg)](#-системные-требования)

</div>

> [!IMPORTANT]
> **SysDiff не является антивирусом.** Он показывает различия между двумя снимками системы и помогает исследовать поведение приложений, но не утверждает, что найденное изменение безопасно или вредоносно.

<img src="assets/screenshots/overview.svg" alt="Пример отчёта SysDiff">

## 🔍 Что такое SysDiff

SysDiff фиксирует состояние выбранных областей Windows, сохраняет снимки в SQLite и сравнивает их по стабильным идентификаторам. Инструмент помогает увидеть, какие файлы, параметры реестра, службы, задачи планировщика, элементы автозагрузки и переменные окружения появились, исчезли или изменились.

Типичный сценарий:

```powershell
sysdiff snapshot create before --profile standard

# Установите или запустите исследуемую программу

sysdiff snapshot create after --profile standard
sysdiff compare before after
```

Для автоматического сценария доступен режим наблюдения:

```powershell
sysdiff watch .\ExampleSetup.exe --arguments "/S"
```

## ✨ Возможности 0.2.0

- 📸 создание именованных снимков Windows;
- 🧩 расширяемая система независимых провайдеров;
- 🗃️ хранение снимков и сравнений в SQLite;
- 🔎 определение `Added`, `Removed` и `Modified`;
- 🚦 уровни важности от `Info` до `Critical`;
- 🧹 режимы фильтрации шума `Raw`, `Balanced` и `Strict`;
- 🖥️ CLI и базовое интерактивное меню;
- 👀 сценарий `watch` для запуска установщика между снимками;
- 📄 консольные, JSON, Markdown и автономные HTML-отчёты;
- 🔐 маскирование чувствительных значений реестра;
- 📦 portable-режим и self-contained ZIP;
- 🧪 unit-тесты и Windows-тест провайдера файловой системы;
- ⚙️ GitHub Actions для сборки, тестов и релизов.

### Реализованные провайдеры

| Провайдер | Что собирается | Статус |
|---|---|---|
| `filesystem` | файлы, каталоги, размер, даты, атрибуты, SHA-256 | MVP |
| `registry` | значения HKCU/HKLM/HKCR, Registry32/Registry64, маскирование секретов | MVP |
| `services` | службы, состояние, путь, тип запуска, учётная запись, зависимости | MVP |
| `scheduled-tasks` | задачи через Windows Task Scheduler COM API, действия и триггеры | MVP |
| `startup` | Run/RunOnce, Startup Folder, Winlogon Shell/Userinit | MVP |
| `environment` | пользовательские и системные переменные, элементы PATH по отдельности | MVP |

## 🚀 Быстрый старт

### Системные требования

- Windows 10 x64 или Windows 11 x64;
- для сборки: .NET 8 SDK;
- PowerShell 7 или Windows PowerShell 5.1 для скриптов;
- права администратора рекомендуются для более полного снимка.

### Сборка из исходников

```powershell
git clone https://github.com/Onmaynec/SysDiff.git
cd SysDiff

dotnet restore
dotnet build
dotnet test
dotnet run --project src/SysDiff.Cli -- --help
```

Или одной командой:

```powershell
.\scripts\build.ps1
```

### Portable-пакет

```powershell
.\scripts\package.ps1
```

Готовый архив появится в `artifacts/`:

```text
SysDiff-0.2.0-win-x64.zip
SysDiff-0.2.0-win-x64.zip.sha256
```

## 🧭 Основные команды

```powershell
# Проверить окружение
sysdiff doctor

# Создать и просмотреть снимки
sysdiff snapshot create before
sysdiff snapshot list
sysdiff snapshot show before
sysdiff snapshot delete before --yes

# Сравнить снимки
sysdiff compare before after
sysdiff compare before after --noise Strict --severity Medium
sysdiff compare before after --format html --output .\report.html
sysdiff compare before after --format json --output .\report.json

# Наблюдать за установщиком
sysdiff watch .\Setup.exe --arguments "/S"
sysdiff watch --no-launch --profile standard

# Профили и каталоги
sysdiff profile list
sysdiff profile show standard
sysdiff config path
```

Полный справочник: [docs/COMMANDS.md](docs/COMMANDS.md).

## 🎛️ Профили сканирования

| Профиль | Назначение | Особенности |
|---|---|---|
| `minimal` | быстрая проверка | службы, задачи, автозагрузка, окружение |
| `standard` | анализ обычного установщика | основные каталоги и разделы реестра, Smart hashing |
| `full` | глубокое исследование | расширенные корни, Full hashing, большой объём данных |

Пример собственного профиля находится в
[`samples/profiles/installer-audit.json`](samples/profiles/installer-audit.json).

## 📊 Отчёты

Автономный HTML-отчёт содержит:

- поиск по всем полям;
- фильтр по категории и важности;
- сводные показатели;
- раскрывающиеся карточки изменений;
- старые и новые значения;
- тёмную и светлую тему;
- адаптивную вёрстку и режим печати;
- экранирование системных строк от HTML-инъекций.

Подробнее: [docs/REPORTS.md](docs/REPORTS.md).

## 🕶️ Конфиденциальность

По умолчанию проект не обязан сохранять имя пользователя или компьютера. Значения реестра с именами вроде `password`, `token`, `secret`, `credential`, `apikey` и `privatekey` заменяются на `<redacted>`, а для сравнения сохраняется SHA-256.

Перед публикацией отчёта всё равно проверьте пути, имена приложений и другие системные данные.

Подробнее: [docs/PRIVACY.md](docs/PRIVACY.md).

## ⚠️ Ограничения

- SysDiff сравнивает два состояния и не перехватывает каждое событие в реальном времени.
- Объекты, созданные и удалённые между снимками, могут не попасть в отчёт.
- Защищённые разделы требуют прав администратора.
- Текущий `watch` ожидает завершения основного процесса и не гарантирует ожидание всех дочерних процессов.
- Проверка подписи драйвера в 0.2.0 подтверждает наличие читаемого сертификата, но не заменяет полную проверку доверия Authenticode.
- Большие профили могут создавать сотни тысяч объектов и занимать значительное место.
- Оценка важности объяснима, но не является вердиктом о вредоносности.

Полный список: [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

## 🏗️ Архитектура

```text
CLI / интерактивное меню
          │
          ▼
SnapshotCoordinator ── ComparisonEngine
          │                 ├── SeverityEngine
          │                 └── NoiseFilterEngine
          ▼
ISnapshotProvider[] ── FileSystem / Registry / Services / Tasks / Startup / Environment
          │
          ▼
      SQLite Store
          │
          ▼
Console / JSON / Markdown / HTML
```

Подробности: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## 🗺️ Roadmap

- **0.1.0:** стабильное ядро снимков, сравнение, основные провайдеры и отчёты;
- **0.2.0:** Firewall, Installed Apps, драйверы, сертификаты, улучшенный `watch`;
- **0.3.0:** live monitor, сетевые события, investigation bundle, SDK провайдеров;
- **1.0.0:** стабильная схема, подписанные релизы и безопасные rollback handlers.

Подробнее: [docs/ROADMAP.md](docs/ROADMAP.md).

## 🤝 Участие в разработке

Идеи, отчёты об ошибках и pull request приветствуются. Перед отправкой изменений прочитайте [CONTRIBUTING.md](CONTRIBUTING.md) и не прикладывайте необработанные снимки с персональными данными.

## 🔐 Безопасность

Уязвимости не следует публиковать в обычных Issues. Инструкции находятся в [SECURITY.md](SECURITY.md).

## 📜 Лицензия

Проект распространяется по лицензии [MIT](LICENSE).

---

<div align="center">

**SysDiff — узнай, что изменилось в Windows.** 🪟🔎

</div>
