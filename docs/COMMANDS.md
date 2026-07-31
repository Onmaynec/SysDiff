# ⌨️ Команды SysDiff

## Общие команды

```powershell
sysdiff --help
sysdiff --version
sysdiff doctor
```

`doctor` проверяет Windows, архитектуру, .NET, права администратора, доступность каталога данных и SQLite.

## Снимки

### Создать

```powershell
sysdiff snapshot create <name>
sysdiff snapshot create before --profile minimal
sysdiff snapshot create before --profile standard
sysdiff snapshot create before --profile full --yes
sysdiff snapshot create before --require-admin
```

Код `7` означает, что снимок сохранён, но один или несколько провайдеров завершились частично.

### Список

```powershell
sysdiff snapshot list
```

### Информация

```powershell
sysdiff snapshot show <name-or-id>
```

### Удаление

```powershell
sysdiff snapshot delete <name-or-id>
sysdiff snapshot delete <name-or-id> --yes
```

## Сравнение

```powershell
sysdiff compare <before> <after>
```

Параметры:

| Параметр | Значения | Назначение |
|---|---|---|
| `--noise` | `Raw`, `Balanced`, `Strict` | фильтрация шума |
| `--severity` | `Info`…`Critical` | минимальная важность |
| `--format` | `console`, `json`, `html`, `markdown` | формат отчёта |
| `--output` | путь | выходной файл |

Примеры:

```powershell
sysdiff compare before after --noise Raw
sysdiff compare before after --severity High
sysdiff compare before after --format html --output .\report.html
sysdiff compare before after --format json --output .\report.json
```

## Наблюдение

```powershell
sysdiff watch .\Setup.exe
sysdiff watch .\Setup.exe --arguments "/S"
sysdiff watch .\Setup.exe --working-directory C:\Installers
sysdiff watch .\Setup.exe --stabilization-delay 10
sysdiff watch --no-launch
```

Поток:

1. создаётся начальный снимок;
2. запускается процесс или ожидается ручная установка;
3. SysDiff ждёт завершения основного процесса;
4. выполняется пауза стабилизации;
5. создаётся итоговый снимок;
6. формируется HTML-отчёт.

> Текущий MVP не гарантирует ожидание всех дочерних процессов установщика.

## Профили

```powershell
sysdiff profile list
sysdiff profile show standard
```

## Конфигурация и пути

```powershell
sysdiff config show
sysdiff config path
```

## Коды завершения

| Код | Значение |
|---:|---|
| 0 | успех |
| 1 | общая ошибка |
| 2 | некорректные аргументы |
| 3 | снимок не найден |
| 5 | доступ запрещён |
| 7 | частичный снимок |
| 8 | отменено |
| 9 | ошибка хранилища |
