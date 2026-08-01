# 🧩 Provider SDK

Provider SDK позволяет создать внешний `ISnapshotProvider`, не изменяя ядро SysDiff.

> [!WARNING]
> Плагин является исполняемым .NET-кодом и работает с правами процесса SysDiff. Загружайте только DLL, которым доверяете.

## Создание

Проект должен ссылаться на:

- `SysDiff.Domain`;
- `SysDiff.ProviderSdk`.

Assembly обязан содержать атрибут:

```csharp
[assembly: SysDiffProviderPlugin(
    ProviderSdkInfo.CurrentVersion,
    DisplayName = "My Provider")]
```

Провайдер:

```csharp
public sealed class MyProvider : ISnapshotProvider
{
    public string Id => "my-provider";
    public string DisplayName => "Мой провайдер";
    public bool RequiresAdministrator => false;

    public Task<ProviderSnapshotResult> CaptureAsync(
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        // Только безопасное чтение данных.
    }
}
```

Полный пример: `samples/plugins/SysDiff.SampleProvider`.

## Запуск

```powershell
sysdiff snapshot create plugin-shot `
  --profile-file .\plugin-profile.json `
  --plugin .\SysDiff.SampleProvider.dll
```

## Правила загрузки

- автоматическое сканирование каталога `plugins` отсутствует;
- путь передаётся пользователем явно;
- версия SDK должна точно совпадать;
- требуется публичный класс с конструктором без параметров;
- ошибка плагина превращает его результат в `Failed`, остальные провайдеры продолжают работу;
- найденные системные строки нельзя выполнять как команды.

## Совместимость

SDK 0.3 имеет версию контракта `0.3`. Несовместимый assembly отклоняется до создания снимка.
