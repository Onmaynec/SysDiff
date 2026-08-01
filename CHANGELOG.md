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

## [0.5.0] — 2026-08-01

### Добавлено

- `SYSDIFF CYBER CONSOLE` с новым ASCII-header и плотным Control Node;
- нумерованный Command Deck `[01]`…`[09]`;
- быстрые клавиши `1–9`, `P/B/A`, `C`, `W`, `L`, `D`;
- boot sequence terminal/storage/providers/engines;
- Action Console со стадиями `queued`, `running`, `completed`, `failed`, `cancelled`;
- animated progress/scanner bars и elapsed time;
- живой Provider Stream при создании снимка;
- единая neon green/cyan/amber/red theme;
- текстовые маркеры `[OK]`, `[>>]`, `[--]`, `[!!]`, `[XX]`, `[//]`;
- `SYSDIFF_NO_ANIMATIONS=1` и `NO_COLOR=1`;
- Cyber Console unit tests и расширенный `--tui-smoke`;
- новое SVG-preview интерфейса.

### Изменено

- spinner длительных операций заменён общей Action Console;
- dashboard стал визуально ближе к NexRoute и использует системные badges;
- selection screens и Change Explorer переведены на единый cyber theme;
- версия EXE, снимков и portable package обновлена до `0.5.0`;
- документация панели полностью переписана.

### Безопасность

- анимации отключаются автоматически в CI и при redirected output;
- boot sequence является только представлением и не запускает дополнительные команды;
- status markers остаются читаемыми без цвета;
- Ctrl+C продолжает корректно отменять операции;
- captured paths, commands и arguments никогда не выполняются интерфейсом.

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
