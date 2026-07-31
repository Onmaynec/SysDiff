# ⚙️ Конфигурация

Основной файл — `sysdiff.json`. Для MVP он поставляется как документированный шаблон; встроенные профили и пути инициализируются кодом.

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

## Профили

Встроенные профили создаются `ProfileCatalog`:

- `minimal`;
- `standard`;
- `full`.

Схема пользовательского профиля:
[`../schemas/profile.schema.json`](../schemas/profile.schema.json).

Пример:
[`../samples/profiles/installer-audit.json`](../samples/profiles/installer-audit.json).

## Приоритет настроек

Целевая модель проекта:

1. встроенные значения;
2. глобальная конфигурация;
3. пользовательская конфигурация;
4. профиль;
5. параметры CLI.

В версии `0.1.0` полностью реализованы встроенные профили и параметры CLI. Загрузка произвольного пользовательского профиля из файла запланирована для `0.2.0`.
