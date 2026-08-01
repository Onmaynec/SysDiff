# 🧰 Решение проблем

## Вместо Cyber Console появляется сообщение об interactive terminal

Интерактивная панель не запускается при перенаправленном вводе или выводе:

```powershell
sysdiff > output.txt
```

Используйте CLI-команду или запустите `sysdiff` напрямую в CMD, PowerShell либо Windows Terminal.

## Окно слишком узкое

При ширине меньше 96 символов включается compact mode. Рекомендуется окно `110×30` или больше.

## Символы рамок отображаются неправильно

Используйте Cascadia Mono, Cascadia Code или Consolas. В legacy console:

```cmd
chcp 65001
```

## Анимация мерцает

```powershell
$env:SYSDIFF_NO_ANIMATIONS = "1"
sysdiff
```

Для отключения цветов:

```powershell
$env:NO_COLOR = "1"
sysdiff
```

## Baseline не настроена

```powershell
sysdiff snapshot list
sysdiff baseline set <snapshot-name-or-id>
sysdiff baseline show
```

Drift Scan не запускается без валидной baseline.

## Baseline исчезла после удаления snapshot

Baseline хранит ссылку на snapshot. Если snapshot удалён, `baseline show` вернёт, что baseline не настроена.

Выберите новый snapshot:

```powershell
sysdiff baseline set trusted-clean
```

## Partial snapshot выбран как baseline

Partial snapshot разрешён, но результат Drift Scan может быть неполным. Предпочтительно:

1. запустить терминал от имени администратора;
2. повторить snapshot;
3. проверить provider warnings;
4. закрепить новый Completed snapshot.

## Drift Risk Score неожиданно высокий

Проверьте:

```powershell
sysdiff timeline list --kind DriftScan
sysdiff compare <baseline> <current> --noise Balanced
```

Score увеличивают Critical/High changes, высокий confidence и несколько затронутых providers. Это приоритет анализа, не malware verdict.

## Drift Risk Score слишком низкий

Возможные причины:

- partial providers;
- низкий confidence;
- Strict noise filter;
- changes помечены как noise;
- baseline и current почти одинаковы.

Откройте JSON summary и HTML comparison report.

## Не удаётся активировать case

Закрытый case нельзя сделать активным. Создайте новый:

```powershell
sysdiff case create "Follow-up"
sysdiff case use "Follow-up"
```

## Закрытие case удалило бы snapshots?

Нет. `case close` меняет только status case и active-case setting. Snapshots, comparisons и reports остаются на месте.

## Timeline содержит старые snapshots

Это ожидаемо. 0.6.0 реконструирует legacy snapshots/comparisons из существующих tables. Данные не дублируются и не переписываются.

## Ошибка unique constraint при создании case

Имена case уникальны без учёта регистра. Используйте другое имя или откройте существующий case:

```powershell
sysdiff case list
sysdiff case show <name>
```

## Access denied

Запустите терминал от имени администратора или используйте `minimal`. SysDiff сохраняет partial snapshot и отображает недоступные providers.

## Snapshot слишком большой

- используйте `minimal`;
- уменьшите roots;
- добавьте excludes;
- используйте `Smart`, а не `Full` hashing;
- снизьте `maximumDepth` и `maximumArtifacts`.

## Watch достиг timeout

SysDiff не завершает исследуемые процессы. Он создаёт итоговый snapshot и report. Повторите с большим timeout.

## Live Monitor не видит короткое событие

Монитор использует polling. Событие, появившееся и исчезнувшее между интервалами, может быть пропущено.

## `.sdshot` не импортируется

Импорт отклоняет повреждённые checksums, неизвестную schema, path traversal, слишком большой archive и несоответствие manifest.

## SQLite повреждена

1. Закройте SysDiff.
2. Скопируйте `sysdiff.db`, `sysdiff.db-wal`, `sysdiff.db-shm`.
3. Не удаляйте исходники до анализа.
4. Для чистого запуска временно переименуйте data directory.

Новые 0.6 tables additive; удаление только investigation tables не рекомендуется без backup.
