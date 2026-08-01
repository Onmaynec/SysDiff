using System.Text.Json;

namespace SysDiff.Cli;

internal sealed class V7CommandRouter
{
    private readonly V6CommandRouter _v6;
    private readonly UpdateService _updates;
    private readonly UpdateSettingsStore _settingsStore;
    private readonly UpdateInstaller _installer;

    public V7CommandRouter(
        V6CommandRouter v6,
        UpdateService updates,
        UpdateSettingsStore settingsStore,
        UpdateInstaller installer)
    {
        _v6 = v6;
        _updates = updates;
        _settingsStore = settingsStore;
        _installer = installer;
    }

    public async Task<int> RunAsync(
        string[] args,
        CommandApp fallback,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            return await _v6.RunAsync(args, fallback, cancellationToken);
        }

        if (args[0] is "--version" or "-v")
        {
            Console.WriteLine($"SysDiff {ProductInfo.Version}");
            return 0;
        }

        if (args[0].Equals("--tui-smoke", StringComparison.OrdinalIgnoreCase))
        {
            PrintSmokeFrame();
            return 0;
        }

        if (args[0].Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            return await RunUpdateAsync(args, cancellationToken);
        }

        int result = await _v6.RunAsync(args, fallback, cancellationToken);
        if (args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
        }

        return result;
    }

    private async Task<int> RunUpdateAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        string command = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
        bool json = HasOption(args, "--json");
        switch (command)
        {
            case "check":
            {
                UpdateCheckResult result = await _updates.CheckAsync(
                    force: true,
                    cancellationToken);
                PrintCheckResult(result, json);
                return 0;
            }
            case "status":
            {
                UpdateCheckResult result = await _updates.GetStatusAsync(cancellationToken);
                PrintCheckResult(result, json);
                return 0;
            }
            case "download":
            {
                UpdateDownloadResult result = await _updates.DownloadLatestAsync(cancellationToken);
                if (json)
                {
                    WriteJson(result);
                }
                else
                {
                    Console.WriteLine($"SysDiff {result.Manifest.Version} загружена и проверена.");
                    Console.WriteLine($"Package: {result.PackagePath}");
                    Console.WriteLine($"Staging: {result.StagingDirectory}");
                    Console.WriteLine("Для установки: sysdiff update install --yes");
                }
                return 0;
            }
            case "install":
            {
                if (!HasOption(args, "--yes"))
                {
                    throw new ArgumentException(
                        "Установка требует явного подтверждения: update install --yes [--restart]");
                }

                UpdateInstallPlan plan = await _installer.ScheduleAsync(
                    HasOption(args, "--restart"),
                    cancellationToken);
                if (json)
                {
                    WriteJson(plan);
                }
                else
                {
                    Console.WriteLine($"Установка SysDiff {plan.Version} запланирована.");
                    Console.WriteLine("Текущий процесс завершится, затем helper заменит EXE и проверит версию.");
                    Console.WriteLine($"Log: {plan.LogPath}");
                }
                return 0;
            }
            case "settings":
                return await RunSettingsAsync(args, json, cancellationToken);
            case "clear-cache":
                await _updates.ClearCacheAsync(cancellationToken);
                Console.WriteLine("Update cache очищен. Настройки автоматической проверки сохранены.");
                return 0;
            default:
                throw new ArgumentException(
                    "Команда update: check, status, download, install, settings или clear-cache.");
        }
    }

    private async Task<int> RunSettingsAsync(
        IReadOnlyList<string> args,
        bool json,
        CancellationToken cancellationToken)
    {
        UpdateSettings settings = await _settingsStore.LoadSettingsAsync(cancellationToken);
        string? autoCheck = GetOption(args, "--auto-check");
        string? autoDownload = GetOption(args, "--auto-download");
        string? interval = GetOption(args, "--interval-hours");
        string? ignoredVersion = GetOption(args, "--ignore");

        if (autoCheck is not null)
        {
            settings = settings with { AutoCheck = ParseBoolean(autoCheck, "--auto-check") };
        }
        if (autoDownload is not null)
        {
            settings = settings with
            {
                AutoDownload = ParseBoolean(autoDownload, "--auto-download")
            };
        }
        if (interval is not null)
        {
            if (!int.TryParse(interval, out int hours) || hours is < 1 or > 168)
            {
                throw new ArgumentException("--interval-hours должен быть числом от 1 до 168.");
            }
            settings = settings with { CheckIntervalHours = hours };
        }
        if (ignoredVersion is not null)
        {
            settings = settings with
            {
                IgnoredVersion = ignoredVersion.Equals("none", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : ReleaseVersion.Parse(ignoredVersion).ToString()
            };
        }

        settings = UpdateSettingsStore.Normalize(settings);
        if (autoCheck is not null
            || autoDownload is not null
            || interval is not null
            || ignoredVersion is not null)
        {
            await _settingsStore.SaveSettingsAsync(settings, cancellationToken);
        }

        if (json)
        {
            WriteJson(settings);
        }
        else
        {
            Console.WriteLine("Update settings");
            Console.WriteLine($"Auto check: {settings.AutoCheck}");
            Console.WriteLine($"Auto download: {settings.AutoDownload}");
            Console.WriteLine($"Interval: {settings.CheckIntervalHours} h");
            Console.WriteLine($"Channel: {settings.Channel}");
            Console.WriteLine($"Ignored version: {settings.IgnoredVersion ?? "-"}");
            Console.WriteLine($"Last checked: {settings.LastCheckedAtUtc?.ToLocalTime().ToString("O") ?? "-"}");
        }
        return 0;
    }

    private static void PrintCheckResult(UpdateCheckResult result, bool json)
    {
        if (json)
        {
            WriteJson(result);
            return;
        }

        Console.WriteLine($"Update status: {result.Status}");
        Console.WriteLine($"Current: {result.CurrentVersion}");
        if (result.Manifest is not null)
        {
            Console.WriteLine($"Latest: {result.Manifest.Version}");
            Console.WriteLine($"Tag: {result.Manifest.Tag}");
            Console.WriteLine($"Asset: {result.Manifest.AssetName}");
            Console.WriteLine($"SHA-256: {result.Manifest.Sha256}");
            Console.WriteLine($"Unsigned: {result.Manifest.Unsigned}");
        }
        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            Console.WriteLine(result.Message);
        }
    }

    private static bool ParseBoolean(string value, string option)
    {
        if (bool.TryParse(value, out bool result))
        {
            return result;
        }

        if (value is "1" or "yes" or "on")
        {
            return true;
        }

        if (value is "0" or "no" or "off")
        {
            return false;
        }

        throw new ArgumentException($"{option} принимает true или false.");
    }

    private static bool HasOption(IReadOnlyList<string> args, string option) =>
        args.Any(value => value.Equals(option, StringComparison.OrdinalIgnoreCase));

    private static string? GetOption(IReadOnlyList<string> args, string option)
    {
        for (int index = 0; index < args.Count; index++)
        {
            if (!args[index].Equals(option, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Для {option} требуется значение.");
            }

            return args[index + 1];
        }
        return null;
    }

    private static void WriteJson<T>(T value)
    {
        Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
    }

    private static void PrintSmokeFrame()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("SYSDIFF CYBER CONSOLE 0.7.0 // RELEASE CHANNEL");
        Console.WriteLine("[03] DRIFT OPS  [05] CASE VAULT  [09] SYSTEM NODE > UPDATE CENTER");
        Console.WriteLine("CURRENT: 0.7.0 | CHANNEL: STABLE | MANIFEST: SHA-256 | INSTALL: ROLLBACK SAFE");
        Console.WriteLine("================================================================================");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """

            RELEASE CHANNEL 0.7
              sysdiff update check [--json]
              sysdiff update status [--json]
              sysdiff update download [--json]
              sysdiff update install --yes [--restart] [--json]
              sysdiff update settings
                  [--auto-check true|false]
                  [--auto-download true|false]
                  [--interval-hours 1..168]
                  [--ignore <version|none>]
              sysdiff update clear-cache

            Автоматическая проверка включена по умолчанию. Автоматическая загрузка выключена.
            Установка всегда требует явного подтверждения и доступна только для sysdiff.exe.
            """);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}
