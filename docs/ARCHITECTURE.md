# 🏗️ Архитектура SysDiff

## Цели

Архитектура SysDiff отделяет Windows-специфичный сбор данных от сравнения, хранения и представления. Ошибка одного провайдера не должна уничтожать весь снимок, а добавление нового источника не должно требовать изменений движка сравнения.

## Слои

### `SysDiff.Domain`

Не зависит от CLI, SQLite и Windows API. Содержит:

- `SystemArtifact` — нормализованный системный объект;
- `SnapshotRecord` и `ProviderSnapshotResult`;
- `SystemChange` и `ComparisonResult`;
- профили и параметры провайдеров;
- интерфейсы `ISnapshotProvider`, `ISnapshotStore`, `ISeverityEngine`, `INoiseFilterEngine`.

### `SysDiff.Core`

Координирует сценарии приложения:

- `SnapshotCoordinator` последовательно запускает включённые провайдеры;
- `ComparisonEngine` сопоставляет объекты по стабильному `Identity`;
- `SeverityEngine` объяснимо присваивает уровень важности;
- `NoiseFilterEngine` скрывает известный системный шум только на уровне отображения;
- `ProfileCatalog` хранит встроенные профили.

### `SysDiff.Providers`

Windows-реализации `ISnapshotProvider`:

- файловая система;
- реестр;
- службы;
- задачи планировщика;
- автозагрузка;
- окружение.

Провайдер возвращает статус, предупреждения, ошибки и список артефактов. Исключение провайдера перехватывается координатором и превращается в частичный результат.

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

`Identity` должен быть стабильным между снимками:

```text
file://C:/Program Files/Example/app.exe
registry://HKLM64/Software/Example/Setting
service://ExampleUpdater
task://Microsoft/Windows/ExampleTask
environment://machine/path/C:/Example/bin
```

## Поток создания снимка

```text
CLI
 │
 ▼
SnapshotCoordinator
 │
 ├─ FileSystemProvider
 ├─ RegistryProvider
 ├─ ServicesProvider
 ├─ ScheduledTasksProvider
 ├─ StartupProvider
 └─ EnvironmentProvider
 │
 ▼
SQLite
```

Снимок сначала сохраняется как `InProgress`. После выполнения провайдеров он становится `Completed`, `Partial`, `Failed` или `Cancelled`.

## Поток сравнения

1. Артефакты двух снимков индексируются по `Identity`.
2. Объект только во втором снимке — `Added`.
3. Объект только в первом снимке — `Removed`.
4. Объект в обоих снимках с отличающимися свойствами — `Modified`.
5. `SeverityEngine` добавляет важность и объяснение.
6. `NoiseFilterEngine` применяет выбранный режим.
7. Результат сохраняется и передаётся рендереру.

## Решения MVP

- Провайдеры выполняются последовательно, чтобы не перегружать диск и реестр.
- SQLite использует WAL и параметризованные запросы.
- `Moved` и `Renamed` пока не определяются.
- Rollback не выполняется автоматически.
- TUI остаётся простым меню без зависимости от внешнего UI-фреймворка.
