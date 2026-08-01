# 📦 Переносимые форматы SysDiff

## `.sdshot`

`.sdshot` — ZIP-контейнер одного снимка:

```text
manifest.json
snapshot.json
checksums.sha256
```

### Экспорт

```powershell
sysdiff snapshot export before --output .\before.sdshot
```

### Импорт

```powershell
sysdiff snapshot import .\before.sdshot
```

### Проверки безопасности

- максимальный размер входного архива — 512 МБ;
- максимальный размер JSON снимка — 1 ГБ;
- допустимы только известные имена файлов без каталогов;
- path traversal и абсолютные пути отклоняются;
- SHA-256 `manifest.json` и `snapshot.json` обязателен;
- схема новее поддерживаемой отклоняется;
- данные архива никогда не выполняются.

## 🧳 Investigation bundle

```powershell
sysdiff bundle create <comparison-id> --output .\investigation.zip
```

Состав:

```text
manifest.json
checksums.sha256
before.sdshot
after.sdshot
report.html
report.json
report.md
```

Bundle не включает сырые логи, дампы памяти или приватные ключи. Перед передачей архива третьей стороне всё равно проверьте имена приложений, сертификатов и системные метаданные.

## Совместимость

Версия 0.3 использует `formatVersion: 1` и `schemaVersion: 1`. Снимки 0.1/0.2 остаются читаемыми, а новые поля JSON допускаются как расширение схемы.
