# 🏗️ Архитектура SysDiff

## Цели

Архитектура SysDiff отделяет Windows-специфичный сбор данных от сравнения, хранения и представления. Ошибка одного провайдера не уничтожает весь снимок, а добавление нового источника не требует изменений движка сравнения или SQLite-схемы.

## Слои

### `SysDiff.Domain`

Не зависит от CLI, SQLite и Windows API. Содержит:

- `SystemArtifact` — нормализованный системный объект;
- `SnapshotRecord` и `ProviderSnapshotResult`;
- `SystemChange` и `ComparisonResult`;
- профили и параметры провайдеров;
- интерфейсы `ISnapshotProvider`, `ISnapshotStore`, `ISeverityEngine`, `INoiseFilterEngine`.

Схема снимка остаётся версии 1: провайдеры 0.2.0 добавляют новые типы артефактов, но не изменяют формат существующих записей.

### `SysDiff.Core`

Координирует сценарии приложения:

- `SnapshotCoordinator` последовательно запускает включённые провайдеры;
- `ComparisonEngine` сопоставляет объекты по стабильному `Identity`;
- `SeverityEngine` объяснимо присваивает уровень важности;
- `NoiseFilterEngine` скрывает известный системный шум только при отображении;
- `ProfileCatalog` хранит встроенные профили.

### `SysDiff.Providers`

Windows-реализации `ISnapshotProvider`:

- файловая система;
- реестр;
- службы;
- задачи планировщика;
- автозагрузка;
- окружение;
- Windows Firewall;
- установленные приложения;
- системные драйверы;
- сертификаты Windows.

Провайдер возвращает статус, предупреждения, ошибки и список артефактов. Исключение провайдера перехватывается координатором и превращается в частичный результат.

#### Изолированный PowerShell-адаптер

`FirewallProvider` и `DriversProvider` используют `PowerShellJsonRunner` только для read-only запросов, которые сложнее надёжно выразить через текущие .NET API.

```text
Provider
   │ заранее определённый сценарий
   ▼
powershell.exe -NoProfile -NonInteractive
   │ JSON stdout + тайм-аут
   ▼
SystemArtifact
```

Адаптер не подставляет в сценарий команды, пути или аргументы, найденные в системе. Данные из Firewall, драйверов, служб, задач и uninstall-разделов никогда не выполняются.

### `SysDiff.Storage`

`SqliteSnapshotStore` хранит:

- заголовки снимков;
- статусы провайдеров;
- нормализованные артефакты;
- сравнения;
- изменения.

Большие коллекции не сериализуются в одну строку: каждый артефакт и изменение хранится отдельной записью.

### `SysDiff.Reporting`

Формирует представления без зависимости от CLI:

- консоль;
- JSON;
- Markdown;
- автономный HTML.

### `SysDiff.Cli`

Содержит разбор команд, DI, базовое интерактивное меню, обработку exit codes и сценарий `watch`.

`ProcessTreeWaiter` использует Toolhelp API для периодического чтения дерева процессов. Он не внедряется в процессы, не перехватывает их код и не завершает их при тайм-ауте.

## Модель артефакта

```csharp
public sealed record SystemArtifact
{
    public required string ProviderId { get; init; }
    public required string ArtifactType { get; init; }
    public required string Identity { get; init; }
    public required string DisplayName { get; init; }
    public Dictionary<string, ArtifactValue> Properties { get; init; }
    public HashSet<string> Tags { get; init; }
}
```

`Identity` стабилен между снимками:

```text
file://C:/Program Files/Example/app.exe
registry://HKLM64/Software/Example/Setting
service://ExampleUpdater
task://Microsoft/Windows/ExampleTask
environment://machine/path/C:/Example/bin
firewall://{rule-name}
app://machine/x64/{product-id}
driver://ExampleDriver
certificate://LocalMachine/Root/{thumbprint}
```

## Поток создания снимка

```text
CLI
 │
 ▼
SnapshotCoordinator
 │
 ├─ FileSystemProvider       ├─ FirewallProvider
 ├─ RegistryProvider         ├─ InstalledAppsProvider
 ├─ ServicesProvider         ├─ DriversProvider
 ├─ ScheduledTasksProvider   └─ CertificatesProvider
 ├─ StartupProvider
 └─ EnvironmentProvider
 │
 ▼
SQLite
```

Снимок сначала создаётся как `InProgress`. После выполнения провайдеров он становится `Completed`, `Partial`, `Failed` или `Cancelled`.

## Поток сравнения

1. Артефакты двух снимков индексируются по `Identity`.
2. Объект только во втором снимке — `Added`.
3. Объект только в первом снимке — `Removed`.
4. Объект в обоих снимках с отличающимися свойствами — `Modified`.
5. `SeverityEngine` добавляет важность и объяснение.
6. `NoiseFilterEngine` применяет выбранный режим.
7. Результат сохраняется и передаётся рендереру.

## Правила безопасности 0.2.0

- приватные ключи сертификатов не читаются и не экспортируются;
- файлы драйверов хешируются потоково;
- проверка сертификата драйвера не выдаётся за полный вердикт Authenticode;
- PowerShell имеет тайм-аут и принимает только JSON;
- `watch --timeout` не завершает исследуемые процессы;
- ошибки доступа локализуются внутри провайдера;
- SQLite использует параметризованные запросы.

## Текущие компромиссы

- Провайдеры выполняются последовательно, чтобы не перегружать диск и системные API.
- `Moved` и `Renamed` пока не определяются.
- Toolhelp может пропустить очень короткоживущий дочерний процесс между опросами.
- Полная проверка доверия Authenticode и сетевой отзыв сертификатов не выполняются.
- Rollback не выполняется автоматически.
- TUI остаётся простым меню без внешнего UI-фреймворка.
