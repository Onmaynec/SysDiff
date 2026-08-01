# ⚙️ Конфигурация

Основной файл — `sysdiff.json`. В версии 0.2.0 он поставляется как документированный шаблон; встроенные профили и пути инициализируются кодом.

## Пример

```json
{
  "schemaVersion": 1,
  "storage": {
    "dataDirectory": "%LocalAppData%\\SysDiff",
    "retentionDays": 90,
    "maximumDatabaseSizeMb": 4096
  },
  "capture": {
    "defaultProfile": "standard",
    "parallelProviders": 1,
    "continueOnProviderError": true
  },
  "comparison": {
    "noiseMode": "Balanced",
    "defaultMinimumSeverity": "Info"
  },
  "privacy": {
    "storeMachineName": false,
    "storeUserName": false,
    "redactUserProfilePath": true,
    "redactSecrets": true
  },
  "ui": {
    "language": "ru",
    "animations": false,
    "theme": "dark"
  }
}
```

JSON Schema: [`../schemas/config.schema.json`](../schemas/config.schema.json).

## Каталоги

Обычный режим:

```text
%LocalAppData%\SysDiff\
├── sysdiff.db
├── logs\
└── reports\
```

Portable-режим включается пустым файлом `portable.mode` рядом с `sysdiff.exe`:

```text
SysDiff\
├── sysdiff.exe
├── portable.mode
├── data\
├── logs\
└── reports\
```

## Встроенные профили

### `minimal`

Быстрый снимок:

- службы и задачи планировщика;
- автозагрузка и окружение;
- Windows Firewall;
- установленные приложения.

### `standard`

Профиль по умолчанию:

- всё из `minimal`;
- выбранные каталоги и разделы реестра;
- системные драйверы;
- сертификаты Windows;
- Smart hashing файлов.

### `full`

Ресурсоёмкий профиль:

- все провайдеры;
- расширенные корни файловой системы и реестра;
- Full hashing;
- увеличенные глубина и лимиты объектов.

Схема будущего пользовательского профиля:
[`../schemas/profile.schema.json`](../schemas/profile.schema.json).

Пример структуры:
[`../samples/profiles/installer-audit.json`](../samples/profiles/installer-audit.json).

## Параметры командной строки

Профиль выбирается так:

```powershell
sysdiff snapshot create before --profile minimal
sysdiff snapshot create before --profile standard
sysdiff snapshot create before --profile full --yes
```

Параметры `watch`, включая тайм-аут и ожидание дерева процессов, описаны в [COMMANDS.md](COMMANDS.md).

## Приоритет настроек

Целевая модель проекта:

1. встроенные значения;
2. глобальная конфигурация;
3. пользовательская конфигурация;
4. профиль;
5. параметры CLI.

В версии `0.2.0` полностью реализованы встроенные профили и параметры CLI. Загрузка произвольного пользовательского профиля из JSON запланирована для `0.3.0`.
