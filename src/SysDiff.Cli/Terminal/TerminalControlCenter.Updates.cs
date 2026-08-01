namespace SysDiff.Cli;

internal sealed partial class TerminalControlCenter
{
    private async Task<bool> RunUpdateCenterAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            UpdateCheckResult status = await _updateService.GetStatusAsync(cancellationToken);
            UpdateSettings settings = await _updateSettingsStore.LoadSettingsAsync(cancellationToken);
            string latest = status.Manifest?.Version ?? "UNKNOWN";
            IReadOnlyList<TerminalMenuItem> actions =
            [
                new("check", "Check stable channel", "получить официальный release manifest", "⌕"),
                new("download", "Download update", "скачать ZIP и проверить SHA-256", "⇩"),
                new("install", "Install downloaded", "backup, replace, verify and rollback", "⇧"),
                new("settings", "Update settings", "auto-check, auto-download and interval", "⚙"),
                new("clear", "Clear update cache", "удалить загруженные пакеты и staging", "×"),
                new("back", "Back", "вернуться в System Node", "←")
            ];

            TerminalMenuItem? action = _renderer.Select(
                "UPDATE CENTER // STABLE RELEASE CHANNEL",
                $"CURRENT: {ProductInfo.Version} // LATEST: {latest} // STATUS: {status.Status} // AUTO: {(settings.AutoCheck ? "ON" : "OFF")}",
                actions,
                value => $"{value.Glyph}  {value.Title,-24} // {value.Description}");
            if (action is null || action.Id == "back")
            {
                return false;
            }

            switch (action.Id)
            {
                case "check":
                {
                    UpdateCheckResult result = await _renderer.RunSpinnerAsync(
                        "UPDATE CENTER // CHECKING STABLE CHANNEL",
                        "Получение и проверка официального release manifest…",
                        () => _updateService.CheckAsync(force: true, cancellationToken),
                        cancellationToken);
                    _renderer.ShowMessage(
                        "UPDATE CHANNEL STATUS",
                        FormatUpdateCheck(result),
                        result.Status == UpdateStatus.Error
                            ? MessageKind.Error
                            : result.Status == UpdateStatus.Available
                                ? MessageKind.Warning
                                : MessageKind.Success);
                    break;
                }
                case "download":
                {
                    UpdateDownloadResult result = await _renderer.RunSpinnerAsync(
                        "UPDATE CENTER // VERIFIED DOWNLOAD",
                        "Загрузка ZIP, SHA-256 verification и безопасная распаковка…",
                        () => _updateService.DownloadLatestAsync(cancellationToken),
                        cancellationToken);
                    _renderer.ShowMessage(
                        "UPDATE READY",
                        $"SysDiff {result.Manifest.Version} загружена и проверена.{Environment.NewLine}{Environment.NewLine}Package:{Environment.NewLine}{result.PackagePath}{Environment.NewLine}{Environment.NewLine}Staging:{Environment.NewLine}{result.StagingDirectory}",
                        MessageKind.Success);
                    break;
                }
                case "install":
                {
                    if (!_updateInstaller.CanSelfUpdate(out string reason))
                    {
                        _renderer.ShowMessage(
                            "SELF-UPDATE UNAVAILABLE",
                            reason,
                            MessageKind.Warning);
                        break;
                    }

                    UpdateCheckResult currentStatus =
                        await _updateService.GetStatusAsync(cancellationToken);
                    if (currentStatus.Status != UpdateStatus.Downloaded)
                    {
                        _renderer.ShowMessage(
                            "UPDATE NOT STAGED",
                            "Сначала выберите Download update. Установка не скачивает непроверенные файлы автоматически.",
                            MessageKind.Warning);
                        break;
                    }

                    bool restart = _renderer.Confirm(
                        "RESTART AFTER UPDATE",
                        "Запустить обновлённую SysDiff после успешной установки?",
                        defaultValue: true);
                    if (!_renderer.Confirm(
                        "INSTALL VERIFIED UPDATE",
                        "SysDiff завершит текущую сессию. Helper создаст backup, заменит EXE, проверит версию и выполнит rollback при ошибке. Продолжить?"))
                    {
                        break;
                    }

                    UpdateInstallPlan plan = await _updateInstaller.ScheduleAsync(
                        restart,
                        cancellationToken);
                    _renderer.ShowMessage(
                        "UPDATE INSTALL SCHEDULED",
                        $"Версия: {plan.Version}{Environment.NewLine}Backup: {plan.BackupExecutable}{Environment.NewLine}Log: {plan.LogPath}{Environment.NewLine}{Environment.NewLine}После закрытия панели начнётся безопасная замена EXE.",
                        MessageKind.Success);
                    return true;
                }
                case "settings":
                    await RunUpdateSettingsAsync(cancellationToken);
                    break;
                case "clear":
                    if (_renderer.Confirm(
                        "CLEAR UPDATE CACHE",
                        "Удалить загруженные release packages и staging? Настройки auto-check сохранятся."))
                    {
                        await _updateService.ClearCacheAsync(cancellationToken);
                        _renderer.ShowMessage(
                            "UPDATE CACHE CLEARED",
                            _paths.UpdatesDirectory,
                            MessageKind.Success);
                    }
                    break;
            }
        }
    }

    private async Task RunUpdateSettingsAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            UpdateSettings settings = await _updateSettingsStore.LoadSettingsAsync(cancellationToken);
            IReadOnlyList<TerminalMenuItem> actions =
            [
                new("auto-check", "Toggle auto-check", $"сейчас: {(settings.AutoCheck ? "ON" : "OFF")}", "⌕"),
                new("auto-download", "Toggle auto-download", $"сейчас: {(settings.AutoDownload ? "ON" : "OFF")}", "⇩"),
                new("interval", "Check interval", $"сейчас: {settings.CheckIntervalHours} h", "◷"),
                new("ignore", "Clear ignored version", $"сейчас: {settings.IgnoredVersion ?? "NONE"}", "×"),
                new("back", "Back", "вернуться в Update Center", "←")
            ];

            TerminalMenuItem? action = _renderer.Select(
                "UPDATE SETTINGS // STABLE CHANNEL",
                "Auto-check не блокирует запуск. Auto-download только загружает и проверяет; установка всегда требует подтверждения.",
                actions,
                value => $"{value.Glyph}  {value.Title,-24} // {value.Description}");
            if (action is null || action.Id == "back")
            {
                return;
            }

            switch (action.Id)
            {
                case "auto-check":
                    settings = settings with { AutoCheck = !settings.AutoCheck };
                    break;
                case "auto-download":
                    settings = settings with { AutoDownload = !settings.AutoDownload };
                    break;
                case "interval":
                    int hours = _renderer.ReadNumber(
                        "UPDATE SETTINGS // INTERVAL",
                        "Интервал автоматической проверки в часах (1–168).",
                        settings.CheckIntervalHours,
                        1,
                        168);
                    settings = settings with { CheckIntervalHours = hours };
                    break;
                case "ignore":
                    settings = settings with { IgnoredVersion = null };
                    break;
            }

            await _updateSettingsStore.SaveSettingsAsync(settings, cancellationToken);
        }
    }

    private static string FormatUpdateCheck(UpdateCheckResult result)
    {
        string latest = result.Manifest?.Version ?? "unknown";
        string published = result.Manifest is null
            ? "-"
            : result.Manifest.PublishedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
        string integrity = result.Manifest is null
            ? "-"
            : $"SHA-256 {result.Manifest.Sha256}";
        return $"Status: {result.Status}{Environment.NewLine}Current: {result.CurrentVersion}{Environment.NewLine}Latest: {latest}{Environment.NewLine}Published: {published}{Environment.NewLine}Integrity: {integrity}{Environment.NewLine}Authenticode: {(result.Manifest?.Unsigned == true ? "unsigned build" : "signed")}{Environment.NewLine}{Environment.NewLine}{result.Message ?? string.Empty}";
    }
}
