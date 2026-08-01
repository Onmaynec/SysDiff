# 🧩 Провайдеры снимков

Каждый источник реализует `ISnapshotProvider`. Ошибка отдельного источника не уничтожает снимок: результат становится `Partial`, а причина сохраняется в предупреждениях.

## Встроенные providers

| ID | Источник |
|---|---|
| `filesystem` | файлы, каталоги, метаданные, SHA-256, reparse points |
| `registry` | HKCU/HKLM/HKCR/HKU/HKCC, x86/x64, secret redaction |
| `services` | службы, путь, аккаунт, тип запуска, зависимости |
| `scheduled-tasks` | задачи, actions, triggers, XML и привилегии |
| `startup` | Run/RunOnce, Startup Folder, Winlogon |
| `environment` | user/machine variables и отдельные PATH entries |
| `firewall` | направление, действие, profiles, ports, addresses, program |
| `installed-apps` | uninstall registry, user/machine, x86/x64 |
| `drivers` | состояние, путь, версия, SHA-256 и сведения о подписи |
| `certificates` | Windows stores, сроки, EKU и локальное доверие |
| `network-configuration` | adapters, DNS, gateways, addresses, proxy и routes |

## 🌐 NetworkConfigurationProvider

Провайдер использует нативный `NetworkInterface` для adapters и read-only PowerShell для `Get-NetRoute`.

Собирает:

- имя, тип и operational status адаптера;
- speed и MAC address;
- DNS suffix и DNS servers;
- gateways и unicast addresses;
- proxy текущего пользователя;
- destination prefix, next hop, metric, protocol и state маршрута.

Он не изменяет DNS, proxy, routes или interfaces.

## 🔐 Защита данных

- FileSystem использует потоковое хеширование и не следует по reparse point;
- Registry маскирует password/token/secret/credential/API key;
- uninstall commands, service paths и task actions никогда не выполняются;
- приватные ключи сертификатов не читаются;
- PowerShell получает только заранее определённый read-only script и возвращает JSON;
- все пути проходят через `PrivacyRedactor` до сохранения снимка.

## 🧩 Внешние providers

Provider SDK описан в [PROVIDER_SDK.md](PROVIDER_SDK.md).

```powershell
sysdiff snapshot create plugin-shot --profile-file .\plugin-profile.json `
  --plugin .\Provider.dll
```

Плагин обязан:

1. содержать совместимый `SysDiffProviderPluginAttribute`;
2. реализовать публичный `ISnapshotProvider`;
3. иметь конструктор без параметров;
4. использовать стабильные `Id` и `Identity`;
5. поддерживать `CancellationToken`;
6. не выполнять найденные системные данные.

Плагины не обнаруживаются и не загружаются автоматически.
