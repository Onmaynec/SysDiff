using SysDiff.Domain;

namespace SysDiff.Cli;

internal sealed partial class TerminalControlCenter
{
    private async Task RunDriftOperationsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TerminalMenuItem> actions =
        [
            new("scan", "Run Drift Scan", "снять текущее состояние и сравнить с baseline", "⌁"),
            new("baseline", "Baseline Vault", "закрепить доверенный снимок", "◆"),
            new("timeline", "Investigation Timeline", "хронология снимков и сравнений", "◷"),
            new("cases", "Case Vault", "локальные кейсы, заметки и связи", "▣"),
            new("back", "Back", "вернуться в Cyber Control Node", "←")
        ];

        while (true)
        {
            BaselineRecord? baseline = await _investigationStore.GetBaselineAsync(cancellationToken);
            InvestigationCaseRecord? activeCase =
                await _investigationStore.GetActiveCaseAsync(cancellationToken);
            string status = $"BASELINE: {baseline?.SnapshotName ?? "NOT SET"} // ACTIVE CASE: {activeCase?.Name ?? "NONE"}";
            TerminalMenuItem? action = _renderer.Select(
                "DRIFT OPERATIONS // CONTROL CHANNEL",
                status,
                actions,
                value => $"{value.Glyph}  {value.Title,-24} // {value.Description}");
            if (action is null || action.Id == "back")
            {
                return;
            }

            switch (action.Id)
            {
                case "scan":
                    await RunDriftScanTuiAsync(cancellationToken);
                    break;
                case "baseline":
                    await RunBaselineVaultAsync(cancellationToken);
                    break;
                case "timeline":
                    await RunInvestigationTimelineAsync(cancellationToken);
                    break;
                case "cases":
                    await RunCaseVaultAsync(cancellationToken);
                    break;
            }
        }
    }

    private async Task RunBaselineVaultAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            BaselineRecord? baseline = await _investigationStore.GetBaselineAsync(cancellationToken);
            IReadOnlyList<TerminalMenuItem> actions = baseline is null
                ?
                [
                    new("set", "Set baseline", "выбрать доверенный снимок", "+"),
                    new("back", "Back", "вернуться в Drift Operations", "←")
                ]
                :
                [
                    new("show", "Show baseline", "открыть metadata активной baseline", "i"),
                    new("set", "Replace baseline", "выбрать другой доверенный снимок", "↻"),
                    new("clear", "Clear baseline", "снять активную baseline", "×"),
                    new("back", "Back", "вернуться в Drift Operations", "←")
                ];

            TerminalMenuItem? action = _renderer.Select(
                "BASELINE VAULT",
                baseline is null
                    ? "Доверенная baseline не настроена."
                    : $"PINNED: {baseline.SnapshotName} // {baseline.SetAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}",
                actions,
                value => $"{value.Glyph}  {value.Title} — {value.Description}");
            if (action is null || action.Id == "back")
            {
                return;
            }

            if (action.Id == "show" && baseline is not null)
            {
                _renderer.ShowMessage(
                    "BASELINE // PINNED SNAPSHOT",
                    $"Snapshot: {baseline.SnapshotName}{Environment.NewLine}ID: {baseline.SnapshotId:D}{Environment.NewLine}Установлена: {baseline.SetAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}Заметка: {baseline.Note ?? "-"}",
                    MessageKind.Success);
            }
            else if (action.Id == "set")
            {
                await SetBaselineFromTuiAsync(cancellationToken);
            }
            else if (action.Id == "clear"
                && _renderer.Confirm("CLEAR BASELINE", "Снять активную baseline? Снимок не будет удалён."))
            {
                await _driftOperations.ClearBaselineAsync(cancellationToken);
                _renderer.ShowMessage("BASELINE CLEARED", "Baseline снята. Снимок сохранён в SQLite.", MessageKind.Success);
            }
        }
    }

    private async Task SetBaselineFromTuiAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SnapshotRecord> snapshots = (await _store.ListSnapshotsAsync(cancellationToken))
            .Where(value => value.Status is SnapshotStatus.Completed or SnapshotStatus.Partial)
            .OrderByDescending(value => value.CreatedAtUtc)
            .ToArray();
        SnapshotRecord? selected = _renderer.Select(
            "BASELINE VAULT // SELECT SNAPSHOT",
            "Partial snapshot разрешён, но Drift Scan покажет предупреждение о неполных данных.",
            snapshots,
            value => $"{value.Name,-30} {value.Status,-9} {value.ProfileName,-10} {value.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
        if (selected is null)
        {
            return;
        }
        if (selected.Status == SnapshotStatus.Partial
            && !_renderer.Confirm(
                "PARTIAL BASELINE",
                "Выбран частичный снимок. Продолжить и закрепить его как baseline?"))
        {
            return;
        }

        string? note = _renderer.ReadText(
            "BASELINE NOTE",
            "Необязательная заметка о доверенном состоянии.",
            string.Empty,
            allowEmpty: true);
        BaselineRecord baseline = await _driftOperations.SetBaselineAsync(
            selected.Id.ToString("D"),
            note,
            cancellationToken);
        _renderer.ShowMessage(
            "BASELINE PINNED",
            $"Доверенный снимок: {baseline.SnapshotName}{Environment.NewLine}ID: {baseline.SnapshotId:D}",
            MessageKind.Success);
    }

    private async Task RunDriftScanTuiAsync(CancellationToken cancellationToken)
    {
        BaselineRecord? baseline = await _investigationStore.GetBaselineAsync(cancellationToken);
        if (baseline is null)
        {
            _renderer.ShowMessage(
                "DRIFT SCAN BLOCKED",
                "Сначала настройте доверенную baseline в Baseline Vault.",
                MessageKind.Warning);
            return;
        }

        CaptureProfile? profile = _renderer.Select(
            "DRIFT SCAN // PROFILE",
            $"BASELINE: {baseline.SnapshotName}",
            _profiles.All.OrderBy(value => value.Name).ToArray(),
            value => $"{value.Name,-12} {value.Description}");
        if (profile is null)
        {
            return;
        }
        if (profile.Name.Equals("full", StringComparison.OrdinalIgnoreCase)
            && !_renderer.Confirm("RESOURCE WARNING", "Full-профиль может занять много времени. Продолжить?"))
        {
            return;
        }

        string? noiseText = _renderer.Select(
            "DRIFT SCAN // NOISE FILTER",
            "Balanced рекомендуется для регулярного контроля дрейфа.",
            new[] { "Balanced", "Strict", "Raw" },
            value => value);
        if (noiseText is null)
        {
            return;
        }

        _renderer.ShowMessage(
            "DRIFT SCAN // ACQUIRING CURRENT STATE",
            $"Baseline: {baseline.SnapshotName}{Environment.NewLine}Profile: {profile.Name}{Environment.NewLine}Noise: {noiseText}{Environment.NewLine}{Environment.NewLine}Инициализация Provider Stream…",
            MessageKind.Info,
            pause: false);
        var progress = new InlineProgress<SnapshotProgress>(_renderer.ShowSnapshotProgress);
        DriftScanResult result = await _driftOperations.ScanAsync(
            profile,
            Enum.Parse<NoiseMode>(noiseText, ignoreCase: true),
            progress,
            cancellationToken);
        Console.WriteLine();
        _renderer.RenderDriftSummary(result);
    }

    private async Task RunInvestigationTimelineAsync(CancellationToken cancellationToken)
    {
        string? filter = _renderer.Select(
            "INVESTIGATION TIMELINE // FILTER",
            "Выберите тип событий или откройте единую ленту.",
            new[] { "All", "DriftScan", "Snapshot", "Comparison", "Case", "Note", "Back" },
            value => value);
        if (filter is null || filter == "Back")
        {
            return;
        }

        TimelineEventKind? kind = filter == "All"
            ? null
            : Enum.Parse<TimelineEventKind>(filter, ignoreCase: true);
        IReadOnlyList<TimelineEventRecord> events =
            await _investigationStore.ListTimelineAsync(250, kind, cancellationToken);
        TimelineEventRecord? selected = _renderer.Select(
            "INVESTIGATION TIMELINE",
            $"FILTER: {filter.ToUpperInvariant()} // EVENTS: {events.Count:N0}",
            events,
            value =>
            {
                string severity = value.Severity?.ToString() ?? "-";
                return $"{value.TimestampUtc.ToLocalTime():MM-dd HH:mm:ss} [{value.Kind,-10}] [{severity,-8}] {value.Title}";
            });
        if (selected is null)
        {
            return;
        }

        string metadata = selected.Metadata.Count == 0
            ? "Metadata: -"
            : string.Join(
                Environment.NewLine,
                selected.Metadata.OrderBy(value => value.Key).Select(value => $"{value.Key}: {value.Value}"));
        _renderer.ShowMessage(
            $"TIMELINE EVENT // {selected.Kind}",
            $"{selected.Title}{Environment.NewLine}{Environment.NewLine}Time: {selected.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}Status: {selected.Status}{Environment.NewLine}Severity: {selected.Severity?.ToString() ?? "-"}{Environment.NewLine}Reference: {selected.ReferenceId ?? "-"}{Environment.NewLine}Case: {selected.CaseId?.ToString("D") ?? "-"}{Environment.NewLine}{Environment.NewLine}{metadata}");
    }

    private async Task RunCaseVaultAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TerminalMenuItem> actions =
        [
            new("create", "Create case", "новый локальный кейс расследования", "+"),
            new("browse", "Browse cases", "просмотр, активация и закрытие", "▣"),
            new("clear", "Clear active case", "новые операции не будут привязываться", "×"),
            new("back", "Back", "вернуться в Cyber Control Node", "←")
        ];

        while (true)
        {
            InvestigationCaseRecord? active =
                await _investigationStore.GetActiveCaseAsync(cancellationToken);
            TerminalMenuItem? action = _renderer.Select(
                "CASE VAULT",
                $"ACTIVE CASE: {active?.Name ?? "NONE"}",
                actions,
                value => $"{value.Glyph}  {value.Title,-24} // {value.Description}");
            if (action is null || action.Id == "back")
            {
                return;
            }
            if (action.Id == "create")
            {
                await CreateCaseFromTuiAsync(cancellationToken);
            }
            else if (action.Id == "browse")
            {
                await BrowseCasesAsync(cancellationToken);
            }
            else if (action.Id == "clear")
            {
                await _investigationStore.SetActiveCaseAsync(null, cancellationToken);
                _renderer.ShowMessage("CASE CHANNEL CLEARED", "Активный кейс снят.", MessageKind.Success);
            }
        }
    }

    private async Task CreateCaseFromTuiAsync(CancellationToken cancellationToken)
    {
        string? name = _renderer.ReadText("CREATE CASE", "Введите короткое название кейса.");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        string description = _renderer.ReadText(
            "CREATE CASE // DESCRIPTION",
            "Описание. Пустая строка разрешена.",
            string.Empty,
            allowEmpty: true) ?? string.Empty;
        string tagsText = _renderer.ReadText(
            "CREATE CASE // TAGS",
            "Теги через запятую. Пустая строка разрешена.",
            string.Empty,
            allowEmpty: true) ?? string.Empty;
        var item = new InvestigationCaseRecord
        {
            Name = name,
            Description = description,
            Tags = new HashSet<string>(
                tagsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase)
        };
        InvestigationCaseRecord created = await _investigationStore.CreateCaseAsync(item, cancellationToken);
        await _investigationStore.SetActiveCaseAsync(created.Id, cancellationToken);
        _renderer.ShowMessage(
            "CASE CREATED",
            $"Кейс «{created.Name}» создан и сделан активным.{Environment.NewLine}ID: {created.Id:D}",
            MessageKind.Success);
    }

    private async Task BrowseCasesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<InvestigationCaseRecord> cases =
            await _investigationStore.ListCasesAsync(cancellationToken);
        InvestigationCaseRecord? selected = _renderer.Select(
            "CASE VAULT // BROWSE",
            "Open cases отображаются первыми.",
            cases,
            value => $"{value.Name,-30} {value.Status,-6} tags:{value.Tags.Count,2} updated:{value.UpdatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
        if (selected is null)
        {
            return;
        }
        InvestigationCaseRecord item = await _investigationStore.GetCaseAsync(
            selected.Id.ToString("D"),
            cancellationToken) ?? selected;
        IReadOnlyList<TerminalMenuItem> actions = item.Status == InvestigationCaseStatus.Open
            ?
            [
                new("details", "Details", "metadata, tags and linked objects", "i"),
                new("use", "Make active", "привязывать новые Drift Scans", "▶"),
                new("close", "Close case", "закрыть без удаления снимков", "×"),
                new("back", "Back", "вернуться к списку", "←")
            ]
            :
            [
                new("details", "Details", "metadata, tags and linked objects", "i"),
                new("back", "Back", "вернуться к списку", "←")
            ];
        TerminalMenuItem? action = _renderer.Select(
            $"CASE // {item.Name}",
            $"STATUS: {item.Status} // LINKS: {item.Links.Count:N0}",
            actions,
            value => $"{value.Glyph}  {value.Title} — {value.Description}");
        if (action is null || action.Id == "back")
        {
            return;
        }
        if (action.Id == "details")
        {
            string links = item.Links.Count == 0
                ? "Связи: -"
                : string.Join(Environment.NewLine, item.Links.Select(value => $"[{value.Kind}] {value.DisplayName}"));
            _renderer.ShowMessage(
                "CASE DETAILS",
                $"Name: {item.Name}{Environment.NewLine}ID: {item.Id:D}{Environment.NewLine}Status: {item.Status}{Environment.NewLine}Created: {item.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}{Environment.NewLine}Tags: {(item.Tags.Count == 0 ? "-" : string.Join(", ", item.Tags))}{Environment.NewLine}{Environment.NewLine}{item.Description}{Environment.NewLine}{Environment.NewLine}{links}");
        }
        else if (action.Id == "use")
        {
            await _investigationStore.SetActiveCaseAsync(item.Id, cancellationToken);
            _renderer.ShowMessage("CASE CHANNEL ACTIVE", $"Активный кейс: {item.Name}", MessageKind.Success);
        }
        else if (action.Id == "close"
            && _renderer.Confirm("CLOSE CASE", "Закрыть кейс? Связанные снимки и отчёты останутся на месте."))
        {
            await _investigationStore.UpdateCaseAsync(
                item with { Status = InvestigationCaseStatus.Closed },
                cancellationToken);
            InvestigationCaseRecord? active =
                await _investigationStore.GetActiveCaseAsync(cancellationToken);
            if (active?.Id == item.Id)
            {
                await _investigationStore.SetActiveCaseAsync(null, cancellationToken);
            }
            _renderer.ShowMessage("CASE CLOSED", item.Name, MessageKind.Success);
        }
    }

    private async Task<bool> RunSystemNodeAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TerminalMenuItem> actions =
        [
            new("updates", "Update Center", "stable releases, download and safe install", "⇧"),
            new("doctor", "Node Diagnostics", "Windows, SQLite, providers and terminal", "✓"),
            new("settings", "System Settings", "paths, storage and Command Deck", "⚙"),
            new("about", "About Node", "version, purpose and safety", "i"),
            new("disconnect", "Disconnect", "закрыть SysDiff", "×"),
            new("back", "Back", "вернуться в Cyber Control Node", "←")
        ];
        TerminalMenuItem? action = _renderer.Select(
            "SYSTEM NODE",
            "Системные функции и безопасное завершение локальной сессии.",
            actions,
            value => $"{value.Glyph}  {value.Title,-22} // {value.Description}");
        if (action is null || action.Id == "back")
        {
            return false;
        }
        if (action.Id == "updates")
        {
            return await RunUpdateCenterAsync(cancellationToken);
        }
        if (action.Id == "doctor")
        {
            await RunDiagnosticsAsync(cancellationToken);
        }
        else if (action.Id == "settings")
        {
            RunSettings();
        }
        else if (action.Id == "about")
        {
            RunAbout();
        }
        else if (action.Id == "disconnect")
        {
            return _renderer.Confirm("DISCONNECT NODE", "Завершить локальную сессию SysDiff?");
        }
        return false;
    }
}
