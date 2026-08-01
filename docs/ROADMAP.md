# 🗺️ Roadmap SysDiff

Roadmap отражает направление развития. Каждая незавершённая задача оформлена отдельным GitHub Issue с критериями готовности.

## 0.1.0 — ядро MVP ✅

- [x] снимки и SQLite;
- [x] Added/Removed/Modified;
- [x] основные Windows-провайдеры;
- [x] severity, noise filters и отчёты;
- [x] TUI, portable package, tests и CI.

## 0.2.0 — расширение покрытия ✅

- [x] FirewallProvider;
- [x] InstalledAppsProvider;
- [x] DriversProvider;
- [x] CertificatesProvider;
- [x] улучшенный `watch`;
- [x] новые severity/noise rules.

## 0.3.0 — расследования ✅

- [x] live process monitor — #3;
- [x] сетевые события и NetworkConfigurationProvider — #4;
- [x] portable investigation bundle — #5;
- [x] экспорт и импорт `.sdshot` — #6;
- [x] пользовательские профили — #7;
- [x] сравнение снимков разных компьютеров — #8;
- [x] обнаружение перемещений и переименований — #9;
- [x] Provider SDK — #10;
- [x] автоматическое маскирование `%USERPROFILE%` — #11.

## 1.0.0 — стабильность

- [ ] стабильная схема данных — #12;
- [ ] миграции между версиями — #13;
- [ ] подписанные релизы и attestations — #14;
- [ ] безопасные rollback preview/handlers — #15;
- [ ] оптимизация снимков с миллионами объектов — #16;
- [ ] полная локализация RU/EN — #17;
- [ ] документированная обратная совместимость — #18.
