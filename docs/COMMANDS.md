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

## Наблюдение 👀

```powershell
sysdiff watch .\Setup.exe
sysdiff watch .\Setup.exe --arguments "/S"
sysdiff watch .\Setup.exe --working-directory C:\Installers
sysdiff watch .\Setup.exe --wait-for-children
sysdiff watch .\Setup.exe --wait-for-children --timeout 900
sysdiff watch .\Setup.exe --stabilization-delay 10 --noise Strict
sysdiff watch --no-launch
```

| Параметр | Назначение |
|---|---|
| `--arguments` | аргументы запуска исследуемого файла |
| `--working-directory` | рабочий каталог процесса |
| `--profile` | профиль снимка |
| `--wait-for-children` | ждать основной процесс и обнаруженное дерево потомков |
| `--timeout <seconds>` | прекратить ожидание после указанного количества секунд и перейти к итоговому снимку |
| `--stabilization-delay <seconds>` | пауза перед итоговым снимком |
| `--noise` | режим фильтрации итогового сравнения |
| `--report` | путь автономного HTML-отчёта |
| `--no-launch` | ручной режим без запуска процесса |

Поток:

1. создаётся начальный снимок;
2. запускается процесс или ожидается ручная установка;
3. SysDiff ждёт основной процесс и, при `--wait-for-children`, его обнаруженных потомков;
4. при достижении тайм-аута процессы не завершаются автоматически;
5. выполняется пауза стабилизации;
6. создаётся итоговый снимок;
7. формируется HTML-отчёт.

> Отслеживание дерева использует периодические снимки Toolhelp. Очень короткоживущий дочерний процесс, появившийся и завершившийся между опросами, может не попасть в список.

## Профили

```powershell
sysdiff profile list
sysdiff profile show standard
```

- `minimal`: службы, задачи, автозагрузка, окружение, Firewall и установленные приложения;
- `standard`: всё из minimal, файловая система, реестр, драйверы и сертификаты;
- `full`: все провайдеры с расширенным файловым и реестровым сканированием.

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
| 7 | частичный снимок или тайм-аут `watch` с сохранённым результатом |
| 8 | отменено |
| 9 | ошибка хранилища |
