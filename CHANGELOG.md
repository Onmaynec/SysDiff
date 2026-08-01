# 📝 История изменений

Все заметные изменения SysDiff документируются здесь. Версии следуют Semantic Versioning.

## [Не выпущено]

### Планируется

- стабильная схема данных 1.0;
- миграции;
- подписанные релизы;
- безопасный rollback preview;
- оптимизация больших снимков;
- полная локализация RU/EN.

## [0.4.0] — 2026-08-01

### Добавлено

- полноэкранный `Terminal Control Center` при запуске `sysdiff` без аргументов;
- ASCII-логотип и профессиональная Windows CLI-компоновка;
- управление `↑/↓`, `Enter`, `Esc`, `/`, `F`, `S`, `R`, `E`, `F5`, `Q`;
- компактный режим для узкого терминала;
- Snapshot Center с созданием, просмотром, экспортом, импортом и удалением;
- Comparison Lab с overview, Change Explorer, поиском, фильтром и сортировкой;
- пошаговый Watch Session;
- интерактивный Process/Network Live Monitor;
- Reports & Bundles center;
- Diagnostics screen;
- spinner и snapshot progress animations;
- `--tui-smoke` для CI;
- отдельные TUI unit tests;
- SVG-preview панели в README.

### Изменено

- обычный CLI сохранён для автоматизации, но больше не является единственным интерфейсом;
- interactive launch подавляет console logger, чтобы не ломать панели;
- версия EXE, снимков и portable package обновлена до `0.4.0`.

### Безопасность

- TUI не запускается при redirected stdin/stdout;
- состояние цветов и курсора восстанавливается через `IDisposable`;
- Watch timeout не завершает процессы;
- Live Monitor не читает содержимое трафика;
- опасные удаления требуют подтверждения.

## [0.3.0] — 2026-08-01

### Добавлено

- live process/network monitor;
- network configuration provider;
- `.sdshot`, investigation bundle и custom profiles;
- cross-machine compare, move/rename и Provider SDK;
- автоматическое маскирование `%USERPROFILE%`.

## [0.2.0] — 2026-08-01

### Добавлено

- Windows Firewall, установленные приложения, драйверы и сертификаты;
- ожидание дочерних процессов и timeout `watch`.

## [0.1.0] — 2026-08-01

### Добавлено

- архитектура Domain/Core/Storage/Providers/Reporting/CLI;
- снимки, сравнение, отчёты, portable package, tests и CI.
