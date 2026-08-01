# 🏗️ Архитектура SysDiff 0.3

## Принципы

- Windows-сбор данных отделён от сравнения, хранения и отчётов;
- ошибка одного провайдера не уничтожает снимок;
- системные строки всегда считаются данными;
- SQLite schema 1 остаётся совместимой с 0.1/0.2;
- новые функции расследования изолированы от стабильного CLI.

## Слои

```text
SysDiff.Domain
  └─ модели, enum, интерфейсы, LiveEvent

SysDiff.Core
  ├─ SnapshotCoordinator
  ├─ ComparisonEngine
  ├─ SeverityEngine / NoiseFilterEngine
  ├─ ProfileCatalog / ProfileLoader
  ├─ PrivacyRedactor
  └─ MachineIdentity

SysDiff.Providers
  ├─ filesystem / registry / services / tasks
  ├─ startup / environment / firewall / apps
  ├─ drivers / certificates
  └─ network-configuration

SysDiff.Storage
  ├─ SqliteSnapshotStore
  └─ SnapshotArchiveService (.sdshot)

SysDiff.Reporting
  └─ Console / JSON / Markdown / HTML

SysDiff.ProviderSdk
  └─ явный контракт внешних providers

SysDiff.Cli
  ├─ CommandApp (стабильные команды)
  ├─ V3CommandRouter
  ├─ ProcessLiveMonitor / NetworkLiveMonitor
  ├─ InvestigationBundleService
  └─ PluginProviderLoader
```

## Снимок

`SnapshotCoordinator` запускает providers последовательно, маскирует пути через `PrivacyRedactor` и добавляет privacy-safe metadata artifact:

```text
sysdiff://snapshot/machine
```

Он содержит SHA-256 fingerprint, Windows build и архитектуру. Открытое имя компьютера не сохраняется.

## Сравнение

1. Объекты сопоставляются по `Identity`.
2. Формируются `Added`, `Removed`, `Modified`.
3. Уникальная removed/added пара файлов с одинаковыми SHA-256 и размером может стать `Moved` или `Renamed`.
4. Неоднозначные пары остаются без эвристического объединения.
5. `SeverityEngine` добавляет важность.
6. `NoiseFilterEngine` скрывает шум только при отображении.
7. Cross-machine режим снижает confidence и добавляет предупреждения.

## Live Monitor

Process monitor использует Toolhelp + polling. Network monitor сравнивает соседние состояния `IPGlobalProperties`. Оба режима:

- не устанавливают драйвер;
- не внедряются в процессы;
- не изменяют сеть;
- поддерживают `CancellationToken`;
- ограничивают число событий.

## Переносимые форматы

`SnapshotArchiveService` формирует `.sdshot` с manifest и checksums. `InvestigationBundleService` объединяет два снимка и отчёты. Архивы не выполняются и проверяются до импорта.

## Provider SDK

Плагин загружается только по явному `--plugin`. Проверяются assembly attribute, версия SDK и реализация `ISnapshotProvider`. Плагин работает с правами SysDiff, поэтому автоматическая загрузка запрещена.

## Совместимость

Новые machine metadata хранятся как обычный artifact, поэтому таблицы SQLite не меняются. Поле `MachineFingerprint` восстанавливается из artifact при чтении старой базы.
