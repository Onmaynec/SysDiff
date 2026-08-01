using System.Diagnostics;
using System.Security.Principal;
using SysDiff.Core;
using SysDiff.Domain;
using SysDiff.Reporting;
using SysDiff.Storage;

namespace SysDiff.Cli;

internal sealed partial class TerminalControlCenter
{
    private static readonly IReadOnlyList<TerminalMenuItem> MainMenu =
    [
        new("snapshots", "Snapshot Node", "создание, просмотр и перенос снимков", "◆"),
        new("compare", "Diff Lab", "сравнение и исследование изменений", "◇"),
        new("drift", "Drift Operations", "baseline, risk score и drift scan", "⌁"),
        new("timeline", "Investigation Timeline", "единая хронология расследования", "◷"),
        new("cases", "Case Vault", "кейсы, заметки и связанные объекты", "▣"),
        new("watch", "Watch Operations", "контролируемый запуск программы", "▶"),
        new("live", "Live Signal", "процессы и сетевые endpoints", "●"),
        new("reports", "Report Vault", "готовые отчёты и архивы", "▤"),
        new("system", "System Node", "diagnostics, settings, about, disconnect", "⚙")
    ];

    private readonly AppPaths _paths;
    private readonly ISnapshotStore _store;
    private readonly IInvestigationStore _investigationStore;
    private readonly DriftOperationsService _driftOperations;
    private readonly SnapshotCoordinator _coordinator;
    private readonly ProfileCatalog _profiles;
    private readonly IReadOnlyCollection<ISnapshotProvider> _providers;
    private readonly ComparisonEngine _comparisonEngine;
    private readonly ConsoleReportRenderer _consoleRenderer;
    private readonly JsonReportRenderer _jsonRenderer;
    private readonly MarkdownReportRenderer _markdownRenderer;
    private readonly HtmlReportRenderer _htmlRenderer;
    private readonly SnapshotArchiveService _snapshotArchive;
    private readonly InvestigationBundleService _bundleService;
    private readonly ProcessLiveMonitor _processMonitor;
    private readonly NetworkLiveMonitor _networkMonitor;
    private readonly TerminalRenderer _renderer;

    public TerminalControlCenter(
        AppPaths paths,
        ISnapshotStore store,
        IInvestigationStore investigationStore,
        DriftOperationsService driftOperations,
        SnapshotCoordinator coordinator,
        ProfileCatalog profiles,
        IEnumerable<ISnapshotProvider> providers,
        ComparisonEngine comparisonEngine,
        ConsoleReportRenderer consoleRenderer,
        JsonReportRenderer jsonRenderer,
        MarkdownReportRenderer markdownRenderer,
        HtmlReportRenderer htmlRenderer,
        SnapshotArchiveService snapshotArchive,
        InvestigationBundleService bundleService,
        ProcessLiveMonitor processMonitor,
        NetworkLiveMonitor networkMonitor,
        TerminalRenderer renderer)
    {
        _paths = paths;
        _store = store;
        _investigationStore = investigationStore;
        _driftOperations = driftOperations;
        _coordinator = coordinator;
        _profiles = profiles;
        _providers = providers.ToArray();
        _comparisonEngine = comparisonEngine;
        _consoleRenderer = consoleRenderer;
        _jsonRenderer = jsonRenderer;
        _markdownRenderer = markdownRenderer;
        _htmlRenderer = htmlRenderer;
        _snapshotArchive = snapshotArchive;
        _bundleService = bundleService;
        _processMonitor = processMonitor;
        _networkMonitor = networkMonitor;
        _renderer = renderer;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        if (!TerminalCapabilities.IsInteractive)
        {
            Console.Error.WriteLine(
                "Интерактивная панель SysDiff требует обычный терминал. Используйте sysdiff --help для CLI-команд.");
            return 2;
        }

        using IDisposable session = _renderer.EnterApplicationMode();
        var navigator = new TerminalMenuNavigator(MainMenu.Count);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TerminalDashboardState state = await LoadDashboardStateAsync(cancellationToken);
            _renderer.RenderDashboard(MainMenu, navigator.SelectedIndex, state);
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            TerminalNavigationAction action = navigator.Apply(key.Key);

            if (action is TerminalNavigationAction.Exit or TerminalNavigationAction.Back)
            {
                return 0;
            }

            if (action == TerminalNavigationAction.Refresh)
            {
                continue;
            }

            if (action != TerminalNavigationAction.Activate)
            {
                continue;
            }

            TerminalMenuItem selected = MainMenu[navigator.SelectedIndex];
            try
            {
                switch (selected.Id)
                {
      case "snapshots":
                        await RunSnapshotCenterAsync(cancellationToken);
                        break;
                    case "compare":
                        await RunComparisonLabAsync(cancellationToken);
                        break;
                    case "drift":
                        await RunDriftOperationsAsync(cancellationToken);
                        break;
                    case "timeline":
                        await RunInvestigationTimelineAsync(cancellationToken);
                        break;
                    case "cases":
                        await RunCaseVaultAsync(cancellationToken);
                        break;
                    case "watch":
                        await RunWatchSessionAsync(cancellationToken);
                        break;
                    case "live":
                        await RunLiveMonitorAsync(cancellationToken);
                        break;
                    case "reports":
                        await RunReportsAsync(cancellationToken);
                        break;
                    case "system":
                        if (await RunSystemNodeAsync(cancellationToken))
                        {
                            return 0;
                        }
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _renderer.ShowException("Операция завершилась ошибкой", exception);
            }
        }
    }

    public void PrintSmokeFrame() => _renderer.RenderSmokeFrame();

    private async Task<TerminalDashboardState> LoadDashboardStateAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SnapshotRecord> snapshots = await _store.ListSnapshotsAsync(cancellationToken);
        Directory.CreateDirectory(_paths.ReportsDirectory);
        int reports = Directory.EnumerateFiles(_paths.ReportsDirectory, "*", SearchOption.TopDirectoryOnly).Count();
        BaselineRecord? baseline = await _investigationStore.GetBaselineAsync(cancellationToken);
        InvestigationCaseRecord? activeCase = await _investigationStore.GetActiveCaseAsync(cancellationToken);
        TimelineEventRecord? lastDrift = (await _investigationStore.ListTimelineAsync(
            1,
            TimelineEventKind.DriftScan,
            cancellationToken)).FirstOrDefault();
        int? lastRisk = lastDrift is not null
            && lastDrift.Metadata.TryGetValue("score", out string? scoreText)
            && int.TryParse(scoreText, out int score)
                ? score
                : null;
        return new TerminalDashboardState(
            snapshots.Count,
            reports,
            _providers.Count,
            IsAdministrator(),
            _paths.Portable,
            Environment.OSVersion.Version.ToString(),
            _paths.DataDirectory,
            baseline?.SnapshotName,
            activeCase?.Name,
            lastRisk);
    }

    private async Task RunSnapshotCenterAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TerminalMenuItem> actions =
        [
            new("create", "Create snapshot", "новый системный снимок", "+"),
            new("browse", "Browse snapshots", "просмотр, экспорт и удаление", "◆"),
            new("import", "Import .sdshot", "проверить и добавить переносимый снимок", "⇩"),
            new("back", "Back", "вернуться в dashboard", "←")
        ];

        while (true)
        {
            TerminalMenuItem? action = _renderer.Select(
                "SNAPSHOT CENTER",
                "Выберите действие стрелками. Все снимки хранятся локально в SQLite.",
                actions,
                value => $"{value.Glyph}  {value.Title} — {value.Description}");
            if (action is null || action.Id == "back")
            {
                return;
            }

            switch (action.Id)
            {
                case "create":
                    await CreateSnapshotAsync(cancellationToken);
                    break;
                case "browse":
                    await BrowseSnapshotsAsync(cancellationToken);
                    break;
                case "import":
                    await ImportSnapshotAsync(cancellationToken);
                    break;
            }
        }
    }

    private async Task CreateSnapshotAsync(CancellationToken cancellationToken)
    {
        string? name = _renderer.ReadText(
            "CREATE SNAPSHOT",
            "Введите уникальное имя снимка. Например: before-install или after-install.");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        CaptureProfile? profile = _renderer.Select(
            "CREATE SNAPSHOT",
            "Выберите профиль. Standard подходит для большинства установщиков.",
            _profiles.All.OrderBy(value => value.Name).ToArray(),
            value => $"{value.Name,-12} {value.Description}");
        if (profile is null)
        {
            return;
        }

        if (profile.Name.Equals("full", StringComparison.OrdinalIgnoreCase)
            && !_renderer.Confirm(
                "RESOURCE WARNING",
                "Full-профиль может занять много времени и места. Продолжить?"))
        {
            return;
        }

        SnapshotRecord snapshot = await CaptureWithProgressAsync(
            name,
            profile,
            "Создание системного снимка",
            cancellationToken);
        string providers = string.Join(
            Environment.NewLine,
            snapshot.ProviderResults.Select(result =>
                $"[{result.Status}] {result.DisplayName}: {result.ArtifactCount:N0}"));
        _renderer.ShowMessage(
            "SNAPSHOT SAVED",
            $"{snapshot.Name}{Environment.NewLine}ID: {snapshot.Id}{Environment.NewLine}Статус: {snapshot.Status}{Environment.NewLine}Объектов: {snapshot.Artifacts.Count:N0}{Environment.NewLine}{Environment.NewLine}{providers}",
            snapshot.Status == SnapshotStatus.Completed ? MessageKind.Success : MessageKind.Warning);
    }

    private async Task BrowseSnapshotsAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            IReadOnlyList<SnapshotRecord> snapshots = await _store.ListSnapshotsAsync(cancellationToken);
            SnapshotRecord? selected = _renderer.Select(
                "BROWSE SNAPSHOTS",
                "↑/↓ — выбор снимка, Enter — открыть, Esc — назад.",
                snapshots,
                value => $"{value.Name,-28} {value.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}  {value.ProfileName,-10} {value.Status}");
            if (selected is null)
            {
                return;
            }

            SnapshotRecord snapshot = await _store.GetSnapshotAsync(
                selected.Id.ToString("D"),
                cancellationToken) ?? selected;
            IReadOnlyList<TerminalMenuItem> actions =
            [
                new("details", "Details", "метаданные и providers", "i"),
                new("export", "Export .sdshot", "переносимый архив с SHA-256", "⇧"),
                new("delete", "Delete", "удалить снимок из локальной базы", "×"),
                new("back", "Back", "к списку снимков", "←")
            ];
            TerminalMenuItem? action = _renderer.Select(
                $"SNAPSHOT · {snapshot.Name}",
                $"{snapshot.Artifacts.Count:N0} объектов · {snapshot.Status} · {snapshot.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
                actions,
                value => $"{value.Glyph}  {value.Title} — {value.Description}");
            if (action is null || action.Id == "back")
            {
                continue;
            }

            if (action.Id == "details")
            {
                _renderer.RenderSnapshotDetails(snapshot);
            }
            else if (action.Id == "export")
            {
                await ExportSnapshotAsync(snapshot, cancellationToken);
            }
            else if (action.Id == "delete"
                && _renderer.Confirm(
                    "DELETE SNAPSHOT",
                    $"Безвозвратно удалить снимок «{snapshot.Name}»?"))
            {
                await _renderer.RunSpinnerAsync(
                    "DELETE SNAPSHOT",
                    "Удаление снимка из SQLite…",
                    () => _store.DeleteSnapshotAsync(snapshot.Id.ToString("D"), cancellationToken),
                    cancellationToken);
                _renderer.ShowMessage("SNAPSHOT DELETED", $"Снимок «{snapshot.Name}» удалён.", MessageKind.Success);
            }
        }
    }

    private async Task ExportSnapshotAsync(
        SnapshotRecord snapshot,
        CancellationToken cancellationToken)
    {
        string defaultPath = Path.Combine(_paths.ReportsDirectory, $"{Sanitize(snapshot.Name)}.sdshot");
        string? output = _renderer.ReadText(
            "EXPORT .SDSHOT",
            "Укажите путь итогового файла.",
            defaultPath);
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        string path = await _renderer.RunSpinnerAsync(
            "EXPORT .SDSHOT",
            "Формирование manifest, snapshot и checksums…",
            () => _snapshotArchive.ExportAsync(snapshot, output, cancellationToken),
            cancellationToken);
        _renderer.ShowMessage(
            "EXPORT COMPLETED",
            $"Снимок экспортирован:{Environment.NewLine}{path}",
            MessageKind.Success);
    }

    private async Task ImportSnapshotAsync(CancellationToken cancellationToken)
    {
        string? path = _renderer.ReadText(
            "IMPORT .SDSHOT",
            "Укажите полный путь к файлу .sdshot.");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        SnapshotRecord snapshot = await _renderer.RunSpinnerAsync(
            "IMPORT .SDSHOT",
            "Проверка структуры, схемы и SHA-256…",
            () => _snapshotArchive.ImportAsync(path, cancellationToken),
            cancellationToken);
        _renderer.ShowMessage(
            "IMPORT COMPLETED",
            $"Снимок «{snapshot.Name}» импортирован.{Environment.NewLine}ID: {snapshot.Id}{Environment.NewLine}Объектов: {snapshot.Artifacts.Count:N0}",
            MessageKind.Success);
    }

    private async Task RunComparisonLabAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SnapshotRecord> headers = await _store.ListSnapshotsAsync(cancellationToken);
        if (headers.Count < 2)
        {
            _renderer.ShowMessage(
                "COMPARISON LAB",
                "Для сравнения необходимо минимум два снимка.",
                MessageKind.Warning);
            return;
        }

        SnapshotRecord? beforeHeader = _renderer.Select(
            "COMPARISON LAB · BEFORE",
            "Выберите начальный снимок.",
            headers,
            FormatSnapshotChoice);
        if (beforeHeader is null)
        {
            return;
        }

        SnapshotRecord? afterHeader = _renderer.Select(
            "COMPARISON LAB · AFTER",
            "Выберите итоговый снимок.",
            headers.Where(value => value.Id != beforeHeader.Id).ToArray(),
            FormatSnapshotChoice);
        if (afterHeader is null)
        {
            return;
        }

        SnapshotRecord before = await _store.GetSnapshotAsync(beforeHeader.Id.ToString("D"), cancellationToken)
            ?? throw new InvalidOperationException("Начальный снимок не найден.");
        SnapshotRecord after = await _store.GetSnapshotAsync(afterHeader.Id.ToString("D"), cancellationToken)
            ?? throw new InvalidOperationException("Итоговый снимок не найден.");

        string? noiseText = _renderer.Select(
            "COMPARISON LAB · NOISE",
            "Balanced подходит для обычного анализа. Raw показывает всё.",
            new[] { "Balanced", "Strict", "Raw" },
            value => value);
        if (noiseText is null)
        {
            return;
        }

        NoiseMode noise = Enum.Parse<NoiseMode>(noiseText, ignoreCase: true);
        bool differentMachines = !string.IsNullOrWhiteSpace(before.MachineFingerprint)
            && !string.IsNullOrWhiteSpace(after.MachineFingerprint)
            && !string.Equals(before.MachineFingerprint, after.MachineFingerprint, StringComparison.Ordinal);
        bool crossMachine = differentMachines
            && _renderer.Confirm(
                "CROSS-MACHINE COMPARISON",
                "Снимки созданы на разных компьютерах. Включить безопасный межмашинный режим?");
        if (differentMachines && !crossMachine)
        {
            _renderer.ShowMessage(
                "COMPARISON CANCELLED",
                "Обычное сравнение снимков разных компьютеров отменено.",
                MessageKind.Warning);
            return;
        }

        ComparisonResult comparison = await _renderer.RunSpinnerAsync(
            "COMPARISON LAB",
            "Сопоставление артефактов и расчёт важности…",
            async () =>
            {
                ComparisonResult result = _comparisonEngine.Compare(before, after, noise, crossMachine);
                await _store.SaveComparisonAsync(result, cancellationToken);
                return result;
            },
            cancellationToken);
        _renderer.RenderComparisonSummary(before, after, comparison);

        IReadOnlyList<TerminalMenuItem> actions =
        [
            new("browse", "Explore changes", "категории, фильтр, поиск и details", "◇"),
            new("html", "Export HTML", "автономный интерактивный отчёт", "H"),
            new("json", "Export JSON", "машиночитаемый отчёт", "J"),
            new("markdown", "Export Markdown", "текстовый отчёт", "M"),
            new("bundle", "Create investigation bundle", "снимки и отчёты в ZIP", "B"),
            new("back", "Back", "вернуться в dashboard", "←")
        ];

        while (true)
        {
            TerminalMenuItem? action = _renderer.Select(
                "COMPARISON LAB · RESULT",
                $"{before.Name} → {after.Name} · {comparison.Changes.Count:N0} изменений · ID {comparison.Id}",
                actions,
                value => $"{value.Glyph}  {value.Title} — {value.Description}");
            if (action is null || action.Id == "back")
            {
                return;
            }

            switch (action.Id)
            {
                case "browse":
                    await BrowseChangesAsync(before, after, comparison, cancellationToken);
                    break;
                case "html":
                case "json":
                case "markdown":
                    await ExportComparisonAsync(before, after, comparison, action.Id, cancellationToken);
                    break;
                case "bundle":
                    await CreateBundleAsync(comparison, cancellationToken);
                    break;
            }
        }
    }

    private async Task BrowseChangesAsync(
        SnapshotRecord before,
        SnapshotRecord after,
        ComparisonResult original,
        CancellationToken cancellationToken)
    {
        int selectedIndex = 0;
        string query = string.Empty;
        Severity minimum = Severity.Info;
        bool severitySort = true;
        bool rawMode = original.NoiseMode == NoiseMode.Raw;

        while (true)
        {
            ComparisonResult active = rawMode
                ? _comparisonEngine.Compare(before, after, NoiseMode.Raw, original.CrossMachine)
                : original;
            IEnumerable<SystemChange> filtered = active.Changes.Where(change => change.Severity >= minimum);
            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(change =>
                    change.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || change.Identity.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || change.ProviderId.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || change.Explanation.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            filtered = severitySort
                ? filtered.OrderByDescending(change => change.Severity).ThenBy(change => change.ProviderId).ThenBy(change => change.DisplayName)
                : filtered.OrderBy(change => change.ProviderId).ThenBy(change => change.DisplayName);
            List<SystemChange> visible = filtered.ToList();
            selectedIndex = visible.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, visible.Count - 1);
            _renderer.RenderChangeBrowser(visible, selectedIndex, query, minimum, severitySort, rawMode);
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.UpArrow && visible.Count > 0)
            {
                selectedIndex = (selectedIndex - 1 + visible.Count) % visible.Count;
            }
            else if (key.Key == ConsoleKey.DownArrow && visible.Count > 0)
            {
                selectedIndex = (selectedIndex + 1) % visible.Count;
            }
            else if (key.Key == ConsoleKey.PageUp && visible.Count > 0)
            {
                selectedIndex = Math.Max(0, selectedIndex - 10);
            }
            else if (key.Key == ConsoleKey.PageDown && visible.Count > 0)
            {
                selectedIndex = Math.Min(visible.Count - 1, selectedIndex + 10);
            }
            else if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                return;
            }
            else if (key.Key is ConsoleKey.Oem2 or ConsoleKey.Divide)
            {
                query = _renderer.ReadText(
                    "CHANGE SEARCH",
                    "Введите часть пути, provider ID, identity или объяснения.",
                    query,
                    allowEmpty: true) ?? string.Empty;
                selectedIndex = 0;
            }
            else if (key.Key == ConsoleKey.F)
            {
                minimum = minimum switch
                {
                    Severity.Info => Severity.Low,
                    Severity.Low => Severity.Medium,
                    Severity.Medium => Severity.High,
                    Severity.High => Severity.Critical,
                    _ => Severity.Info
                };
                selectedIndex = 0;
            }
            else if (key.Key == ConsoleKey.S)
            {
                severitySort = !severitySort;
                selectedIndex = 0;
            }
            else if (key.Key == ConsoleKey.R)
            {
                rawMode = !rawMode;
                selectedIndex = 0;
            }
            else if (key.Key == ConsoleKey.E)
            {
                string? format = _renderer.Select(
                    "EXPORT COMPARISON",
                    "Выберите формат отчёта.",
                    new[] { "html", "json", "markdown" },
                    value => value.ToUpperInvariant());
                if (format is not null)
                {
                    await ExportComparisonAsync(before, after, active, format, cancellationToken);
                }
            }
            else if (key.Key == ConsoleKey.Enter && visible.Count > 0)
            {
                ShowChangeDetails(visible[selectedIndex]);
            }
        }
    }

    private void ShowChangeDetails(SystemChange change)
    {
        string properties = change.ChangedProperties.Count == 0
            ? "Изменённые свойства отсутствуют."
            : string.Join(
                Environment.NewLine,
                change.ChangedProperties.Select(property =>
                    $"{property.Name}: {property.Before?.Value ?? "∅"} → {property.After?.Value ?? "∅"}"));
        _renderer.ShowMessage(
            $"{change.ChangeType} · {change.DisplayName}",
            $"Provider: {change.ProviderId}{Environment.NewLine}Severity: {change.Severity}{Environment.NewLine}Confidence: {change.Confidence:P0}{Environment.NewLine}Identity: {change.Identity}{Environment.NewLine}{Environment.NewLine}{change.Explanation}{Environment.NewLine}{change.WhyThisMatters}{Environment.NewLine}{Environment.NewLine}{properties}",
            change.Severity >= Severity.High ? MessageKind.Warning : MessageKind.Info);
    }

    private async Task ExportComparisonAsync(
        SnapshotRecord before,
        SnapshotRecord after,
        ComparisonResult comparison,
        string format,
        CancellationToken cancellationToken)
    {
        string normalized = format.Equals("md", StringComparison.OrdinalIgnoreCase)
            ? "markdown"
            : format.ToLowerInvariant();
        string extension = normalized == "markdown" ? "md" : normalized;
        string defaultPath = Path.Combine(
            _paths.ReportsDirectory,
            $"{Sanitize(before.Name)}-to-{Sanitize(after.Name)}.{extension}");
        string? output = _renderer.ReadText(
            "EXPORT COMPARISON",
            $"Укажите путь отчёта {normalized.ToUpperInvariant()}.",
            defaultPath);
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        string content = normalized switch
        {
            "html" => _htmlRenderer.Render(before, after, comparison),
            "json" => _jsonRenderer.Render(before, after, comparison),
            "markdown" => _markdownRenderer.Render(before, after, comparison),
            "console" => _consoleRenderer.Render(before, after, comparison),
            _ => throw new ArgumentException("Неизвестный формат отчёта.")
        };
        string fullPath = Path.GetFullPath(output);
        await _renderer.RunSpinnerAsync(
            "EXPORT COMPARISON",
            "Формирование отчёта…",
            async () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
                await File.WriteAllTextAsync(fullPath, content, cancellationToken);
            },
            cancellationToken);
        _renderer.ShowMessage(
            "REPORT SAVED",
            $"Отчёт сохранён:{Environment.NewLine}{fullPath}",
            MessageKind.Success);
    }

    private async Task CreateBundleAsync(
        ComparisonResult comparison,
        CancellationToken cancellationToken)
    {
        string defaultPath = Path.Combine(
            _paths.ReportsDirectory,
            $"investigation-{comparison.Id:N}.zip");
        string? output = _renderer.ReadText(
            "INVESTIGATION BUNDLE",
            "Укажите путь ZIP-архива.",
            defaultPath);
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        string path = await _renderer.RunSpinnerAsync(
            "INVESTIGATION BUNDLE",
            "Упаковка снимков, сравнения и отчётов…",
            () => _bundleService.CreateAsync(comparison.Id, output, cancellationToken),
            cancellationToken);
        _renderer.ShowMessage(
            "BUNDLE CREATED",
            $"Investigation bundle создан:{Environment.NewLine}{path}",
            MessageKind.Success);
    }

    private async Task RunWatchSessionAsync(CancellationToken cancellationToken)
    {
        string? mode = _renderer.Select(
            "WATCH SESSION",
            "Выберите способ наблюдения.",
            new[] { "Запустить программу", "Ручной режим", "Назад" },
            value => value);
        if (mode is null || mode == "Назад")
        {
            return;
        }

        CaptureProfile? profile = _renderer.Select(
            "WATCH SESSION · PROFILE",
            "Выберите профиль начального и итогового снимка.",
            _profiles.All.OrderBy(value => value.Name).ToArray(),
            value => $"{value.Name,-12} {value.Description}");
        if (profile is null)
        {
            return;
        }

        bool manual = mode == "Ручной режим";
        string? executable = null;
        string arguments = string.Empty;
        bool waitForChildren = true;
        int timeoutSeconds = 900;
        if (!manual)
        {
            executable = _renderer.ReadText(
                "WATCH SESSION · PROGRAM",
                "Укажите полный путь к EXE, BAT, CMD или MSI launcher.");
            if (string.IsNullOrWhiteSpace(executable))
            {
                return;
            }

            arguments = _renderer.ReadText(
                "WATCH SESSION · ARGUMENTS",
                "Аргументы запуска. Пустая строка разрешена.",
                string.Empty,
                allowEmpty: true) ?? string.Empty;
            waitForChildren = _renderer.Confirm(
                "WATCH SESSION · PROCESS TREE",
                "Ожидать обнаруженное дерево дочерних процессов?",
                defaultValue: true);
            timeoutSeconds = _renderer.ReadNumber(
                "WATCH SESSION · TIMEOUT",
                "Максимальное время ожидания в секундах. 0 = без ограничения.",
                900,
                0,
                86_400);
        }

        int stabilization = _renderer.ReadNumber(
            "WATCH SESSION · STABILIZATION",
            "Пауза перед итоговым снимком в секундах.",
            3,
            0,
            600);
        string session = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        SnapshotRecord before = await CaptureWithProgressAsync(
            $"watch-{session}-before",
            profile,
            "WATCH SESSION · BEFORE",
            cancellationToken);

        bool timedOut = false;
        if (manual)
        {
            _renderer.ShowMessage(
                "WATCH SESSION · MANUAL",
                "Установите или запустите исследуемую программу. Вернитесь в это окно после завершения и нажмите любую клавишу.",
                MessageKind.Info);
        }
        else
        {
            string expanded = Environment.ExpandEnvironmentVariables(executable!);
            string fullExecutable = Path.GetFullPath(expanded);
            if (!File.Exists(fullExecutable))
            {
                throw new FileNotFoundException("Исполняемый файл не найден.", fullExecutable);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = fullExecutable,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(fullExecutable) ?? Environment.CurrentDirectory,
                UseShellExecute = true
            };
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Не удалось запустить исследуемую программу.");
            TimeSpan? timeout = timeoutSeconds > 0 ? TimeSpan.FromSeconds(timeoutSeconds) : null;
            ProcessTreeWaitResult waitResult = await _renderer.RunSpinnerAsync(
                "WATCH SESSION · RUNNING",
                $"PID {process.Id} · ожидание завершения{(waitForChildren ? " дерева процессов" : string.Empty)}…",
                () => ProcessTreeWaiter.WaitAsync(process, waitForChildren, timeout, cancellationToken),
                cancellationToken);
            timedOut = waitResult.TimedOut;
            if (timedOut)
            {
                _renderer.ShowMessage(
                    "WATCH SESSION · TIMEOUT",
                    "Тайм-аут достигнут. SysDiff не завершает процессы автоматически и продолжит с итоговым снимком.",
                    MessageKind.Warning);
            }
        }

        if (stabilization > 0)
        {
            await _renderer.RunSpinnerAsync(
                "WATCH SESSION · STABILIZATION",
                $"Ожидание фоновых операций: {stabilization} сек.…",
                () => Task.Delay(TimeSpan.FromSeconds(stabilization), cancellationToken),
                cancellationToken);
        }

        SnapshotRecord after = await CaptureWithProgressAsync(
            $"watch-{session}-after",
            profile,
            "WATCH SESSION · AFTER",
            cancellationToken);
        ComparisonResult comparison = await _renderer.RunSpinnerAsync(
            "WATCH SESSION · COMPARE",
            "Сопоставление начального и итогового снимков…",
            async () =>
            {
                ComparisonResult result = _comparisonEngine.Compare(before, after, NoiseMode.Balanced);
                await _store.SaveComparisonAsync(result, cancellationToken);
                return result;
            },
            cancellationToken);
        string reportPath = Path.Combine(_paths.ReportsDirectory, $"watch-{session}.html");
        Directory.CreateDirectory(_paths.ReportsDirectory);
        await File.WriteAllTextAsync(
            reportPath,
            _htmlRenderer.Render(before, after, comparison),
            cancellationToken);
        _renderer.ShowMessage(
            "WATCH SESSION COMPLETED",
            $"Изменений: {comparison.Changes.Count:N0}{Environment.NewLine}Скрыто как шум: {comparison.HiddenAsNoise:N0}{Environment.NewLine}Before: {before.Status}{Environment.NewLine}After: {after.Status}{Environment.NewLine}Timeout: {(timedOut ? "да" : "нет")}{Environment.NewLine}{Environment.NewLine}HTML report:{Environment.NewLine}{reportPath}",
            timedOut || before.Status == SnapshotStatus.Partial || after.Status == SnapshotStatus.Partial
                ? MessageKind.Warning
                : MessageKind.Success);
    }

    private async Task RunLiveMonitorAsync(CancellationToken cancellationToken)
    {
        string? category = _renderer.Select(
            "LIVE MONITOR",
            "Process отслеживает запуск/завершение. Network — появление/исчезновение endpoints без чтения трафика.",
            new[] { "Process", "Network", "Назад" },
            value => value);
        if (category is null || category == "Назад")
        {
            return;
        }

        int duration = _renderer.ReadNumber(
            "LIVE MONITOR · DURATION",
            "Длительность наблюдения в секундах.",
            30,
            1,
            86_400);
        int? rootPid = null;
        if (category == "Process"
            && _renderer.Confirm(
                "LIVE MONITOR · ROOT PID",
                "Ограничить наблюдение деревом одного процесса?"))
        {
            rootPid = _renderer.ReadNumber(
                "LIVE MONITOR · ROOT PID",
                "Введите PID корневого процесса.",
                Environment.ProcessId,
                1,
                int.MaxValue);
        }

        IReadOnlyList<LiveEvent> events = category == "Process"
            ? await _renderer.RunSpinnerAsync(
                "LIVE PROCESS MONITOR",
                $"Наблюдение {duration} сек. · Esc недоступен во время атомарного интервала, Ctrl+C отменяет…",
                () => _processMonitor.MonitorAsync(TimeSpan.FromSeconds(duration), rootPid, cancellationToken),
                cancellationToken)
            : await _renderer.RunSpinnerAsync(
                "LIVE NETWORK MONITOR",
                $"Наблюдение {duration} сек. без чтения содержимого трафика…",
                () => _networkMonitor.MonitorAsync(TimeSpan.FromSeconds(duration), cancellationToken),
                cancellationToken);

        Directory.CreateDirectory(_paths.ReportsDirectory);
        string prefix = category.ToLowerInvariant();
        string baseName = $"live-{prefix}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        string jsonPath = Path.Combine(_paths.ReportsDirectory, baseName + ".json");
        await File.WriteAllTextAsync(jsonPath, LiveEventWriter.Render(events, "json"), cancellationToken);
        if (_renderer.Confirm(
            "LIVE MONITOR · EXPORT",
            "Дополнительно сохранить Markdown-журнал?"))
        {
            string markdownPath = Path.Combine(_paths.ReportsDirectory, baseName + ".md");
            await File.WriteAllTextAsync(markdownPath, LiveEventWriter.Render(events, "markdown"), cancellationToken);
        }

        _renderer.RenderLiveEvents(
            $"LIVE {category.ToUpperInvariant()} MONITOR · {events.Count:N0} EVENTS",
            events);
        _renderer.ShowMessage(
            "LIVE JOURNAL SAVED",
            $"JSON-журнал:{Environment.NewLine}{jsonPath}",
            MessageKind.Success);
    }

    private async Task RunReportsAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.ReportsDirectory);
        while (true)
        {
            FileInfo[] files = new DirectoryInfo(_paths.ReportsDirectory)
                .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToArray();
            FileInfo? selected = _renderer.Select(
                "REPORTS & BUNDLES",
                "Выберите файл. HTML открывается системным браузером, остальные — связанным приложением.",
                files,
                file => $"{file.Name,-52} {FormatBytes(file.Length),10}  {file.LastWriteTime:yyyy-MM-dd HH:mm}");
            if (selected is null)
            {
                return;
            }

            string? action = _renderer.Select(
                $"REPORT · {selected.Name}",
                selected.FullName,
                new[] { "Open", "Show path", "Delete", "Back" },
                value => value);
            if (action is null || action == "Back")
            {
                continue;
            }

            if (action == "Open")
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = selected.FullName,
                    UseShellExecute = true
                });
                _renderer.ShowMessage("REPORT OPENED", selected.FullName, MessageKind.Success);
            }
            else if (action == "Show path")
            {
                _renderer.ShowMessage("REPORT PATH", selected.FullName);
            }
            else if (action == "Delete"
                && _renderer.Confirm("DELETE REPORT", $"Удалить «{selected.Name}»?"))
            {
                await Task.Run(selected.Delete, cancellationToken);
                _renderer.ShowMessage("REPORT DELETED", selected.Name, MessageKind.Success);
            }
        }
    }

    private async Task RunDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var diagnostics = new List<TerminalDiagnosticItem>
        {
            new(OperatingSystem.IsWindows() ? TerminalDiagnosticState.Ok : TerminalDiagnosticState.Error, "Windows", Environment.OSVersion.ToString()),
            new(Environment.Is64BitOperatingSystem ? TerminalDiagnosticState.Ok : TerminalDiagnosticState.Warning, "Architecture", Environment.Is64BitOperatingSystem ? "x64" : "x86"),
            new(IsAdministrator() ? TerminalDiagnosticState.Ok : TerminalDiagnosticState.Warning, "Privileges", IsAdministrator() ? "Administrator" : "Standard user"),
            new(TerminalDiagnosticState.Ok, ".NET", Environment.Version.ToString()),
            new(TerminalDiagnosticState.Ok, "Data directory", _paths.DataDirectory),
            new(TerminalDiagnosticState.Ok, "Storage mode", _paths.Portable ? "Portable" : "User profile"),
            new(TerminalDiagnosticState.Ok, "Providers", _providers.Count.ToString("N0"))
        };

        try
        {
            Directory.CreateDirectory(_paths.DataDirectory);
            string probe = Path.Combine(_paths.DataDirectory, $".write-test-{Guid.NewGuid():N}");
            await File.WriteAllTextAsync(probe, "ok", cancellationToken);
            File.Delete(probe);
            diagnostics.Add(new TerminalDiagnosticItem(TerminalDiagnosticState.Ok, "Data write", "доступна"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new TerminalDiagnosticItem(TerminalDiagnosticState.Error, "Data write", exception.Message));
        }

        try
        {
            IReadOnlyList<SnapshotRecord> snapshots = await _store.ListSnapshotsAsync(cancellationToken);
            diagnostics.Add(new TerminalDiagnosticItem(TerminalDiagnosticState.Ok, "SQLite", $"{_paths.DatabasePath} · snapshots {snapshots.Count:N0}"));
        }
        catch (Exception exception)
        {
            diagnostics.Add(new TerminalDiagnosticItem(TerminalDiagnosticState.Error, "SQLite", exception.Message));
        }

        diagnostics.Add(new TerminalDiagnosticItem(
            TerminalCapabilities.IsInteractive ? TerminalDiagnosticState.Ok : TerminalDiagnosticState.Warning,
            "Terminal UI",
            TerminalCapabilities.IsInteractive ? "interactive console detected" : "redirected/non-interactive"));
        diagnostics.Add(new TerminalDiagnosticItem(
            TerminalCapabilities.GetSafeWindowWidth() >= 96 ? TerminalDiagnosticState.Ok : TerminalDiagnosticState.Warning,
            "Window size",
            $"{TerminalCapabilities.GetSafeWindowWidth() + 1}×{TerminalCapabilities.GetSafeWindowHeight()}"));
        _renderer.RenderDiagnostics(diagnostics);
    }

    private void RunSettings()
    {
        _renderer.ShowMessage(
            "SYSTEM SETTINGS // COMMAND DECK",
            $"Режим хранения: {(_paths.Portable ? "portable" : "user profile")}{Environment.NewLine}Данные: {_paths.DataDirectory}{Environment.NewLine}SQLite: {_paths.DatabasePath}{Environment.NewLine}Отчёты: {_paths.ReportsDirectory}{Environment.NewLine}Логи: {_paths.LogsDirectory}{Environment.NewLine}{Environment.NewLine}Горячие клавиши:{Environment.NewLine}↑/↓ — навигация{Environment.NewLine}Enter — открыть{Environment.NewLine}Esc — назад{Environment.NewLine}/ — поиск изменений{Environment.NewLine}F — severity filter{Environment.NewLine}S — сортировка{Environment.NewLine}R — raw changes{Environment.NewLine}E — экспорт{Environment.NewLine}1–9 — открыть модуль напрямую{Environment.NewLine}P/B/A — Snapshot Node{Environment.NewLine}C — Diff Lab{Environment.NewLine}W — Watch Operations{Environment.NewLine}L — Live Signal{Environment.NewLine}D — Node Diagnostics{Environment.NewLine}F5 — обновить Control Node{Environment.NewLine}Q — выход");
    }

    private void RunAbout()
    {
        _renderer.ShowMessage(
            "ABOUT SYSDIFF 0.6.0",
            "SysDiff — локальная Windows-утилита для снимков, сравнения и расследования системных изменений. Cyber Console с Drift Operations является основным интерактивным интерфейсом, а CLI-команды сохранены для автоматизации и CI.\n\nSysDiff не является антивирусом и не выносит вердикт о вредоносности. Live monitor ничего не завершает, не меняет сеть и не читает содержимое трафика.\n\nАвтор: Onmaynec\nЛицензия: MIT");
    }

    private async Task<SnapshotRecord> CaptureWithProgressAsync(
        string name,
        CaptureProfile profile,
        string title,
        CancellationToken cancellationToken)
    {
        _renderer.ShowMessage(
            title,
            $"Снимок «{name}» · профиль {profile.Name}{Environment.NewLine}Инициализация providers…",
            MessageKind.Info,
            pause: false);
        var progress = new InlineProgress<SnapshotProgress>(_renderer.ShowSnapshotProgress);
        SnapshotRecord snapshot = await _coordinator.CaptureAsync(
            name,
            profile,
            _paths.DataDirectory,
            IsAdministrator(),
            progress,
            cancellationToken);
        Console.WriteLine();
        return snapshot;
    }

    private static string FormatSnapshotChoice(SnapshotRecord value) =>
        $"{value.Name,-28} {value.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}  {value.ProfileName,-10} {value.Status}";

    private static string Sanitize(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }

    private static string FormatBytes(long value)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        double amount = value;
        int suffix = 0;
        while (amount >= 1024 && suffix < suffixes.Length - 1)
        {
            amount /= 1024;
            suffix++;
        }

        return $"{amount:0.##} {suffixes[suffix]}";
    }

    private static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}



