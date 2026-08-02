# 🔄 Обновления SysDiff

SysDiff 0.9.0 использует официальный **stable release channel** GitHub. Проверка обновлений выполняется по `release-manifest.json`, опубликованному вместе с каждым GitHub Release.

## Быстрый старт

```powershell
sysdiff update check
sysdiff update download
sysdiff update install --yes --restart
```

Проверить сохранённое состояние без сетевого запроса:

```powershell
sysdiff update status
sysdiff update status --json
```

## Что считается официальным обновлением

Updater принимает manifest только при выполнении всех условий:

- `product` равен `SysDiff`;
- канал равен `stable`;
- версия является стабильной SemVer `X.Y.Z`;
- runtime равен `win-x64`;
- tag имеет вид `vX.Y.Z`;
- asset называется `SysDiff-X.Y.Z-win-x64.zip`;
- HTTPS URL указывает на `github.com/Onmaynec/SysDiff/releases/download/...`;
- размер не превышает установленный лимит;
- SHA-256 содержит 64 hex-символа;
- `minimumUpdaterVersion` поддерживается текущей сборкой.

Manifest с другим host, runtime, channel, tag или asset отклоняется до загрузки.

## Автоматическая проверка

По умолчанию:

```text
channel        stable
autoCheck      true
autoDownload   false
interval       24 часа
autoInstall    никогда
```

Настройки:

```powershell
sysdiff update settings
sysdiff update settings --auto-check false
sysdiff update settings --auto-check true --interval-hours 12
sysdiff update settings --auto-download true
sysdiff update settings --ignore 0.9.0
sysdiff update settings --ignore none
```

Автоматическая проверка:

- запускается только в обычном интерактивном режиме;
- не выполняется в CI;
- ограничена коротким timeout;
- не блокирует открытие Cyber Console при недоступной сети;
- не повторяется чаще настроенного интервала.

`autoDownload=true` разрешает загрузить и проверить архив, но **не устанавливает** его. Установка всегда требует явного подтверждения.

## Update Center

В Cyber Console откройте:

```text
[09] System Node
       └─ Update Center
```

Доступные действия:

- проверить stable channel;
- скачать и проверить release ZIP;
- установить уже загруженное обновление;
- включить или отключить auto-check;
- включить или отключить auto-download;
- изменить интервал;
- очистить update cache.

## Проверка целостности

Перед распаковкой SysDiff проверяет:

1. HTTP Content-Length, если он доступен;
2. фактическое количество загруженных байт;
3. SHA-256 ZIP-архива;
4. безопасные пути ZIP entries;
5. общий размер распакованных данных;
6. наличие `sysdiff.exe`;
7. вывод staged executable `SysDiff <version>`.

Несовпадение SHA-256 удаляет загруженный файл. Непроверенный EXE не передаётся installer helper.

## Безопасная установка

Self-update поддерживается только при запуске опубликованного `sysdiff.exe`.

При запуске через:

```powershell
dotnet run --project .\src\SysDiff.Cli
```

команды проверки и загрузки доступны, но установка отклоняется. Это защищает исходный проект и `dotnet` host от случайной замены.

Установка выполняется так:

```text
verified staging
      ↓
launch helper with ArgumentList
      ↓
wait for current SysDiff PID
      ↓
backup current sysdiff.exe
      ↓
copy pending executable
      ↓
atomic replace
      ↓
run --version verification
      ├─ OK    → remove backup, optional restart
      └─ FAIL  → restore backup
```

Пути передаются helper как отдельные параметры. `Invoke-Expression`, построение командной строки из captured data и запуск содержимого снимков не используются.

## Данные пользователя

Updater не удаляет и не заменяет:

- `sysdiff.db`;
- snapshots;
- investigation cases;
- reports;
- logs;
- пользовательские profiles;
- update settings;
- migration backups.

Обновление EXE и миграция базы являются разными операциями. После установки новой версии existing database не изменяется автоматически: сначала используйте `sysdiff migration plan`, затем при необходимости `migration apply --yes`.

Очищается только локальный update cache при явной команде:

```powershell
sysdiff update clear-cache
```

## Подписи и provenance

Каждый официальный GitHub Release содержит:

- portable ZIP;
- `.sha256`;
- `release-manifest.json`;
- GitHub artifact attestations для ZIP и manifest.

Версия 0.9.0 пока публикуется без Authenticode-сертификата. Это указано явно:

```json
{
  "unsigned": true
}
```

Отсутствие Authenticode не скрывается. SHA-256, официальный GitHub host, tag и provenance проверяют целостность и происхождение release pipeline, но не заменяют будущую code-signing подпись.

## Полное ручное обновление

Когда self-update недоступен:

1. откройте страницу GitHub Releases;
2. скачайте `SysDiff-X.Y.Z-win-x64.zip` и `.sha256`;
3. проверьте SHA-256;
4. закройте SysDiff;
5. распакуйте архив в новый каталог;
6. перенесите только пользовательские данные, если они находились рядом с portable EXE;
7. запустите `sysdiff migration status` перед применением database migrations.

Не заменяйте работающий EXE напрямую.
