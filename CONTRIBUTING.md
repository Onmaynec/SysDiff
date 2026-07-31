# 🤝 Участие в разработке SysDiff

Спасибо за интерес к проекту! SysDiff работает с чувствительными системными данными, поэтому качество, прозрачность и безопасность важнее количества функций.

## 🧭 Перед началом

1. Проверьте существующие Issues и Roadmap.
2. Для большой функции сначала создайте Issue с описанием архитектуры.
3. Не прикладывайте реальные снимки реестра, логи с токенами и пути с персональными данными.
4. Одна ветка и один pull request должны решать одну понятную задачу.

## 🛠️ Локальная разработка

```powershell
git clone https://github.com/Onmaynec/SysDiff.git
cd SysDiff
.\scripts\build.ps1
.\scripts\test.ps1
```

## 🌿 Имена веток

```text
feature/firewall-provider
fix/registry-access
docs/provider-guide
test/path-comparison
```

## ✅ Требования к коду

- nullable reference types включены;
- предупреждения компилятора считаются ошибками;
- длительные операции принимают `CancellationToken`;
- UI не содержит бизнес-логику;
- новые провайдеры реализуют `ISnapshotProvider`;
- секретные значения не попадают в журнал или отчёт;
- для критичной логики добавляются тесты;
- пустые `catch`, псевдокод и незавершённые методы не принимаются.

## 🧪 Проверка перед PR

```powershell
dotnet format --verify-no-changes
dotnet build SysDiff.sln --configuration Release
dotnet test SysDiff.sln --configuration Release
.\scripts\package.ps1
```

## 📝 Описание PR

Укажите:

- что изменено;
- какую проблему это решает;
- как проверялось;
- какие ограничения остались;
- затрагиваются ли конфиденциальность, права администратора или формат данных.
