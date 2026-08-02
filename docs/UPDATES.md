# 🔄 Обновления SysDiff

SysDiff 0.12.0 использует официальный **stable release channel** GitHub. Проверка выполняется по `release-manifest.json`, опубликованному вместе с release assets.

## Быстрый старт

```powershell
sysdiff update check
sysdiff update status --json
sysdiff update download
sysdiff update install --yes --restart
```

## Официальный manifest

Updater принимает manifest только когда:

- `product` равен `SysDiff`;
- channel — `stable`;
- version — stable SemVer `X.Y.Z`;
- runtime — `win-x64`;
- tag совпадает с `vX.Y.Z`;
- asset — `SysDiff-X.Y.Z-win-x64.zip`;
- URL указывает на официальный GitHub release path;
- size и SHA-256 корректны;
- `minimumUpdaterVersion` поддерживается.

Другой host, runtime, channel, tag или asset отклоняется до установки.

## Настройки

```powershell
sysdiff update settings
sysdiff update settings --auto-check false
sysdiff update settings --auto-check true --interval-hours 12
sysdiff update settings --auto-download true
sysdiff update settings --ignore 0.12.0
sysdiff update settings --ignore none
```

Defaults:

```text
channel        stable
autoCheck      true
autoDownload   false
interval       24 часа
autoInstall    никогда
```

Auto-download не устанавливает update. Install всегда требует `--yes`.

## Проверка ZIP

Перед staging проверяются HTTP/final size, SHA-256, безопасные ZIP paths, unpacked size limit, наличие `sysdiff.exe` и staged `SysDiff <version>` output.

## Безопасная установка

```text
verified staging
      ↓
helper with structured ArgumentList
      ↓
wait current PID
      ↓
backup current executable
      ↓
replace
      ↓
post-install --version verification
      ├─ OK    → cleanup / optional restart
      └─ FAIL  → rollback executable
```

Self-update доступен опубликованному `sysdiff.exe`. При `dotnet run` check/download разрешены, install отклоняется.

## Данные пользователя

Updater не удаляет:

- `sysdiff.db`;
- snapshots и comparisons;
- cases и timeline;
- reports/logs/profiles;
- update settings;
- migration backups;
- Legacy Bridge backups;
- Scale Lab NDJSON datasets и benchmark results;
- public schemas и fixtures.

Обновление EXE не применяет database или portable migrations автоматически и не запускает Scale benchmark. После установки используйте независимые checks:

```powershell
sysdiff migration status
sysdiff schema list
sysdiff legacy matrix
sysdiff scale matrix
```

Legacy conversion запускается только явно:

```powershell
sysdiff legacy plan comparison .\old-report.json
sysdiff legacy convert comparison .\old-report.json --yes
```

Scale Lab работает с обычными файлами и не импортирует их автоматически:

```powershell
sysdiff scale compare .\before.ndjson .\after.ndjson --output .\changes.ndjson
```

## Package 0.12

Portable ZIP включает:

```text
SysDiff-0.12.0-win-x64.zip
SCALE_LAB.txt
LEGACY_BRIDGE.txt
legacy-fixtures/v0.9/*.json
SCHEMA_CONTRACT.txt
schemas/public/v1/*.schema.json
schema-fixtures/v1/*.json
MIGRATIONS.txt
COMPATIBILITY.txt
UPDATES.txt
```

## Подписи и provenance

Official release содержит ZIP, `.sha256`, `release-manifest.json` и GitHub artifact attestations.

Версия 0.12.0 пока без Authenticode:

```json
{
  "unsigned": true
}
```

SHA-256 и provenance подтверждают integrity/origin pipeline, но не заменяют code-signing сертификат.

## Ручное обновление

1. скачайте ZIP и `.sha256` из official GitHub Release;
2. проверьте hash;
3. закройте SysDiff;
4. распакуйте в новый каталог;
5. сохраните пользовательские data directories, backups и Scale Lab datasets;
6. запустите `migration status`, `schema list`, `legacy matrix` и `scale matrix`.

Не заменяйте работающий EXE напрямую.
