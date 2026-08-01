# 🧰 Решение проблем

## `Access denied`

Запустите терминал от имени администратора или используйте более узкий профиль. Недоступный provider переводит снимок в `Partial`, но данные остальных источников сохраняются.

## Снимок слишком большой

- используйте `minimal`;
- уменьшите roots;
- добавьте exclude patterns;
- выберите `Smart`, а не `Full`;
- снизьте `maximumDepth` и `maximumArtifacts`.

## Системный шум

```powershell
sysdiff compare before after --noise Balanced
sysdiff compare before after --noise Strict
sysdiff compare before after --noise Raw
```

## Live monitor ничего не обнаружил

Polling может пропустить событие, появившееся и завершившееся между опросами. Увеличьте `--duration` и запускайте monitor до исследуемой программы.

```powershell
sysdiff live process --duration 120
sysdiff live network --duration 120
```

## `.sdshot` не импортируется

Проверьте:

- расширение и размер файла;
- наличие `manifest.json`, `snapshot.json`, `checksums.sha256`;
- не изменялся ли архив после экспорта;
- поддерживается ли `schemaVersion`;
- доступна ли запись в SQLite.

Повреждённые checksums и небезопасные пути отклоняются намеренно.

## Профиль отклонён

```powershell
sysdiff profile load .\profile.json
```

Ошибка показывает неизвестный provider или некорректный limit. Поддерживаются только providers, зарегистрированные в текущем процессе, включая явно переданные plugins.

## Плагин не загружается

Проверьте:

- точный путь DLL;
- `SysDiffProviderPluginAttribute`;
- SDK version `0.3`;
- публичный класс `ISnapshotProvider`;
- публичный конструктор без параметров;
- совпадение архитектуры и target framework.

Не копируйте случайные DLL в `plugins`: автоматическая загрузка отсутствует.

## Cross-machine сравнение не показывает режим

Старые снимки 0.1/0.2 могли не содержать machine fingerprint. Для надёжного определения источника создайте новые снимки 0.3 или импортируйте `.sdshot` 0.3.

## Move/Rename не определён

Эвристика требует уникальную removed/added пару файлов с одинаковыми SHA-256 и размером. При дубликатах SysDiff оставляет `Added` и `Removed`, чтобы избежать ложного совпадения.

## Investigation bundle не создаётся

Проверьте, что comparison ID существует и оба исходных снимка ещё находятся в базе. Bundle не может восстановить удалённый снимок только из записи changes.

## База SQLite повреждена

1. Закройте SysDiff.
2. Скопируйте `sysdiff.db`, `sysdiff.db-wal`, `sysdiff.db-shm`.
3. Не удаляйте оригиналы до анализа.
4. Для чистого запуска временно переименуйте каталог данных.

## Отчёт содержит личные данные

Не публикуйте его. `%USERPROFILE%` маскируется автоматически, но названия приложений, сертификаты, IP-адреса и данные plugins всё равно требуют ручной проверки.
