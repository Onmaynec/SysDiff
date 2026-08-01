# ⌨️ Команды SysDiff 0.3

## Общие

```powershell
sysdiff --help
sysdiff --version
sysdiff doctor
```

## 📸 Снимки

```powershell
sysdiff snapshot create <name> --profile minimal|standard|full
sysdiff snapshot create <name> --profile-file .\profile.json
sysdiff snapshot list
sysdiff snapshot show <name-or-id>
sysdiff snapshot delete <name-or-id> --yes
```

### Переносимый `.sdshot`

```powershell
sysdiff snapshot export <name-or-id> --output .\snapshot.sdshot
sysdiff snapshot import .\snapshot.sdshot
```

Экспорт содержит manifest, JSON снимка и SHA-256. Импорт отклоняет неизвестную схему, повреждённую checksum, небезопасный путь и превышение лимита размера.

## 🔎 Сравнение

```powershell
sysdiff compare <before> <after>
sysdiff compare before after --noise Raw|Balanced|Strict
sysdiff compare before after --severity Info|Low|Medium|High|Critical
sysdiff compare before after --format console|json|html|markdown
sysdiff compare pc-a pc-b --cross-machine
```

`--cross-machine` включает явный межмашинный режим. SysDiff предупреждает о разных Windows build/architecture и снижает confidence.

Уникальная пара удалённого и добавленного файла с одинаковыми SHA-256 и размером может стать `Moved` или `Renamed`. Неоднозначные пары не объединяются.

## 👀 Watch

```powershell
sysdiff watch .\Setup.exe --arguments "/S"
sysdiff watch .\Setup.exe --wait-for-children --timeout 900
sysdiff watch .\Setup.exe --stabilization-delay 10 --noise Strict
sysdiff watch --no-launch
```

Тайм-аут прекращает ожидание, но **не завершает** исследуемые процессы.

## 🔴 Live process monitor

```powershell
sysdiff live process --duration 60
sysdiff live process --duration 120 --root-pid 1234
sysdiff live process --duration 60 --format markdown --output .\process-events.md
```

Параметры:

| Параметр | Назначение |
|---|---|
| `--duration` | длительность 1–86400 секунд |
| `--root-pid` | показывать обнаруженное дерево указанного PID |
| `--format` | `json`, `markdown` |
| `--output` | путь результата |

## 🌐 Live network monitor

```powershell
sysdiff live network --duration 60
sysdiff live network --format json --output .\network-events.json
```

Фиксируются изменения TCP connections и UDP listeners. Payload пакетов не читается.

## 🧳 Investigation bundle

```powershell
sysdiff bundle create <comparison-id>
sysdiff bundle create <comparison-id> --output .\investigation.zip
```

Bundle содержит:

```text
manifest.json
checksums.sha256
before.sdshot
after.sdshot
report.html
report.json
report.md
```

## 🎛️ Пользовательские профили

```powershell
sysdiff profile load .\profile.json
sysdiff snapshot create custom --profile-file .\profile.json
```

Проверяются имя, список известных провайдеров, `maximumDepth` и `maximumArtifacts`.

## 🧩 Provider SDK

```powershell
sysdiff snapshot create plugin-shot --profile-file .\plugin-profile.json `
  --plugin .\Provider.dll
```

`--plugin` можно передать несколько раз. DLL загружается только явно, должна содержать `SysDiffProviderPluginAttribute`, совместимую версию SDK и публичный `ISnapshotProvider` с конструктором без параметров.

## ⚙️ Профили и конфигурация

```powershell
sysdiff profile list
sysdiff profile show standard
sysdiff config show
sysdiff config path
```

## Коды завершения

| Код | Значение |
|---:|---|
| 0 | успех |
| 1 | общая ошибка |
| 2 | некорректные аргументы или plugin/profile validation |
| 3 | снимок не найден |
| 5 | доступ запрещён |
| 7 | частичный снимок |
| 8 | отменено |
| 9 | ошибка хранилища |
