# ⚙️ Конфигурация

Основной шаблон — `sysdiff.json`. Встроенные пути и профили инициализируются кодом; версия 0.3 также поддерживает пользовательский профиль из JSON.

## Каталоги

Обычный режим:

```text
%LocalAppData%\SysDiff\
├── sysdiff.db
├── logs\
└── reports\
```

Portable-режим включается файлом `portable.mode` рядом с `sysdiff.exe`:

```text
SysDiff\
├── sysdiff.exe
├── portable.mode
├── data\
├── logs\
├── reports\
├── profiles\
└── plugins\
```

Каталог `plugins` не сканируется автоматически.

## Встроенные профили

- `minimal`: службы, задачи, startup, environment, Firewall, apps, network configuration;
- `standard`: всё из minimal + filesystem, registry, drivers, certificates;
- `full`: расширенные roots и Full hashing.

## Пользовательский профиль

```powershell
sysdiff profile load .\profile.json
sysdiff snapshot create custom --profile-file .\profile.json
```

Пример:

```json
{
  "name": "installer-audit",
  "description": "Проверка установщика",
  "providers": {
    "filesystem": {
      "enabled": true,
      "roots": ["%ProgramFiles%", "%LocalAppData%"],
      "exclude": ["**\\Cache\\**"],
      "hashMode": "Smart",
      "maximumDepth": 8,
      "maximumArtifacts": 250000
    },
    "registry": {
      "enabled": true,
      "roots": ["HKCU\\Software", "HKLM64\\Software"]
    },
    "network-configuration": {
      "enabled": true
    }
  }
}
```

Проверки:

- непустое `name`;
- минимум один provider;
- provider должен быть встроенным или явно загруженным plugin;
- `maximumDepth`: 0–256;
- `maximumArtifacts`: 1–5 000 000;
- некорректный JSON показывает путь проблемного поля.

Схема: [`../schemas/profile.schema.json`](../schemas/profile.schema.json). Пример: [`../samples/profiles/installer-audit.json`](../samples/profiles/installer-audit.json).

## Конфиденциальность

Версия 0.3 применяет маскирование `%USERPROFILE%` до сохранения артефактов. Имя компьютера не сохраняется открыто; для межмашинного сравнения используется SHA-256 fingerprint.

## Приоритет

1. встроенные значения;
2. выбранный встроенный профиль или `--profile-file`;
3. параметры конкретной CLI-команды.

Полная загрузка всех полей `sysdiff.json` как runtime overrides остаётся задачей будущей стабильной конфигурации.
