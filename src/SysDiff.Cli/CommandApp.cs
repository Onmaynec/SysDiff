using System.Diagnostics;
using System.Security.Principal;
using SysDiff.Core;
using SysDiff.Domain;
using SysDiff.Reporting;

namespace SysDiff.Cli;

public sealed class CommandApp
{
    private readonly AppPaths _paths;
    private readonly ISnapshotStore _store;
    private readonly SnapshotCoordinator _coordinator;
    private readonly ProfileCatalog _profiles;
    private readonly ComparisonEngine _comparisonEngine;
    private readonly ConsoleReportRenderer _consoleRenderer;
    private readonly JsonReportRenderer _jsonRenderer;
    private readonly MarkdownReportRenderer _markdownRenderer;
    private readonly HtmlReportRenderer _htmlRenderer;

    public CommandApp(
        AppPaths paths,
        ISnapshotStore store,
        SnapshotCoordinator coordinator,
        ProfileCatalog profiles,
        ComparisonEngine comparisonEngine,
        ConsoleReportRenderer consoleRenderer,
        JsonReportRenderer jsonRenderer,
        MarkdownReportRenderer markdownRenderer,
        HtmlReportRenderer htmlRenderer)
    {
        _paths = paths;
        _store = store;
        _coordinator = coordinator;
        _profiles = profiles;
        _comparisonEngine = comparisonEngine;
        _consoleRenderer = consoleRenderer;
        _jsonRenderer = jsonRenderer;
        _markdownRenderer = markdownRenderer;
        _htmlRenderer = htmlRenderer;
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            return await RunInteractiveAsync(cancellationToken);
        }

        if (args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
            return 0;
        }

        if (args[0] is "--version" or "-v")
        {
            Console.WriteLine("SysDiff 0.1.0");
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "snapshot" => await RunSnapshotAsync(args[1..], cancellationToken),
            "compare" => await RunCompareAsync(args[1..], cancellationToken),
            "watch" => await RunWatchAsync(args[1..], cancellationToken),
            "doctor" => await RunDoctorAsync(cancellationToken),
            "profile" => RunProfile(args[1..]),
            "config" => RunConfig(args[1..]),
            _ => UnknownCommand(args[0])
        };
    }

    private async Task<int> RunSnapshotAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("Укажите подкоманду snapshot.");
        }

        return args[0].ToLowerInvariant() switch
        {
            "create" => await CreateSnapshotAsync(args[1..], cancellationToken),
            "list" => await ListSnapshotsAsync(cancellationToken),
            "show" => await ShowSnapshotAsync(args[1..], cancellationToken),
            "delete" => await DeleteSnapshotAsync(args[1..], cancellationToken),
            _ => throw new ArgumentException($"Неизвестная подкоманда snapshot: {args[0]}")
        };
    }

    private async Task<int> CreateSnapshotAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var reader = new ArgumentReader(args);
        string name = RequirePosition(reader, 0, "Укажите имя снимка.");
        string profileName = reader.Get("profile", "standard");
        CaptureProfile profile = _profiles.Get(profileName);

        if (profile.Name.Equals("full", StringComparison.OrdinalIgnoreCase)
            && !reader.Has("yes"))
        {
            Console.WriteLine("⚠ Профиль full может занять много времени и места.");
            Console.Write("Продолжить? [y/N]: ");
            string? answer = Console.ReadLine();
            if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return 8;
            }
        }

        if (reader.Has("require-admin") && !IsAdministrator())
        {
            Console.Error.WriteLine("Для этой операции требуются права администратора.");
            return 5;
        }

        var progress = new Progress<SnapshotProgress>(value =>
        {
            string current = string.IsNullOrWhiteSpace(value.CurrentItem)
                ? string.Empty
                : $" · {Shorten(value.CurrentItem, 90)}";
            Console.Write($"\r[{value.ProviderId}] {value.Processed:N0}{current}      ");
        });

        Console.WriteLine($"Создание снимка «{name}» с профилем «{profile.Name}»…");

        SnapshotRecord snapshot = await _coordinator.CaptureAsync(
            name,
            profile,
            _paths.DataDirectory,
            IsAdministrator(),
            progress,
            cancellationToken);

        Console.WriteLine();
        Console.WriteLine(
            $"Снимок сохранён: {snapshot.Id} · {snapshot.Artifacts.Count:N0} объектов · {snapshot.Status}");

        foreach (ProviderSnapshotResult result in snapshot.ProviderResults)
        {
            Console.WriteLine(
                $"[{StatusMarker(result.Status)}] {result.DisplayName}: {result.ArtifactCount:N0} объектов");
        }

        return snapshot.Status == SnapshotStatus.Partial ? 7 : 0;
    }

    private async Task<int> ListSnapshotsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SnapshotRecord> snapshots =
            await _store.ListSnapshotsAsync(cancellationToken);

        if (snapshots.Count == 0)
        {
            Console.WriteLine("Снимков пока нет.");
            return 0;
        }

        Console.WriteLine("Имя                         Дата                       Профиль      Статус");
        Console.WriteLine(new string('─', 82));

        foreach (SnapshotRecord snapshot in snapshots)
        {
            Console.WriteLine(
                $"{Shorten(snapshot.Name, 28),-28} {snapshot.CreatedAtUtc:yyyy-MM-dd HH:mm:ss zzz}  {snapshot.ProfileName,-12} {snapshot.Status}");
        }

        return 0;
    }

    private async Task<int> ShowSnapshotAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var reader = new ArgumentReader(args);
        string value = RequirePosition(reader, 0, "Укажите имя или ID снимка.");
        SnapshotRecord? snapshot = await _store.GetSnapshotAsync(value, cancellationToken);

        if (snapshot is null)
        {
            Console.Error.WriteLine("Снимок не найден.");
            return 3;
        }

        Console.WriteLine($"Снимок: {snapshot.Name}");
        Console.WriteLine($"ID: {snapshot.Id}");
        Console.WriteLine($"Создан: {snapshot.CreatedAtUtc:O}");
        Console.WriteLine($"Профиль: {snapshot.ProfileName}");
        Console.WriteLine($"Статус: {snapshot.Status}");
        Console.WriteLine($"Объектов: {snapshot.Artifacts.Count:N0}");
        Console.WriteLine();

        foreach (ProviderSnapshotResult result in snapshot.ProviderResults)
        {
            Console.WriteLine(
                $"[{StatusMarker(result.Status)}] {result.DisplayName,-24} {result.ArtifactCount,10:N0}");
        }

        return 0;
    }

    private async Task<int> DeleteSnapshotAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var reader = new ArgumentReader(args);
        string value = RequirePosition(reader, 0, "Укажите имя или ID снимка.");

        if (!reader.Has("yes"))
        {
            Console.Write($"Удалить снимок «{value}»? [y/N]: ");
            string? answer = Console.ReadLine();
            if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
            {
                return 8;
            }
        }

        await _store.DeleteSnapshotAsync(value, cancellationToken);
        Console.WriteLine("Снимок удалён.");
        return 0;
    }

    private async Task<int> RunCompareAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var reader = new ArgumentReader(args);
        string beforeValue = RequirePosition(reader, 0, "Укажите начальный снимок.");
        string afterValue = RequirePosition(reader, 1, "Укажите итоговый снимок.");

        SnapshotRecord? before = await _store.GetSnapshotAsync(beforeValue, cancellationToken);
        SnapshotRecord? after = await _store.GetSnapshotAsync(afterValue, cancellationToken);

        if (before is null || after is null)
        {
            Console.Error.WriteLine("Один или оба снимка не найдены.");
            return 3;
        }

        NoiseMode noiseMode = Enum.TryParse(
            reader.Get("noise", "Balanced"),
            ignoreCase: true,
            out NoiseMode parsedNoise)
            ? parsedNoise
            : throw new ArgumentException("Допустимые режимы шума: Raw, Balanced, Strict.");

        ComparisonResult comparison = _comparisonEngine.Compare(before, after, noiseMode);

        if (reader.Get("severity") is { Length: > 0 } minimumText)
        {
            if (!Enum.TryParse(minimumText, ignoreCase: true, out Severity minimum))
            {
                throw new ArgumentException("Неизвестный уровень важности.");
            }

            comparison = comparison with
            {
                Changes = comparison.Changes
                    .Where(x => x.Severity >= minimum)
                    .ToList()
            };
        }

        await _store.SaveComparisonAsync(comparison, cancellationToken);

        string format = reader.Get("format", "console").ToLowerInvariant();
        string content = format switch
        {
            "console" => _consoleRenderer.Render(before, after, comparison),
            "json" => _jsonRenderer.Render(before, after, comparison),
            "html" => _htmlRenderer.Render(before, after, comparison),
            "markdown" or "md" => _markdownRenderer.Render(before, after, comparison),
            _ => throw new ArgumentException("Формат должен быть console, json, html или markdown.")
        };

        string? output = reader.Get("output");
        if (string.IsNullOrWhiteSpace(output) && format != "console")
        {
            string extension = format == "markdown" ? "md" : format;
            output = Path.Combine(
                _paths.ReportsDirectory,
                $"{SanitizeFileName(before.Name)}-to-{SanitizeFileName(after.Name)}.{extension}");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            Console.Write(content);
        }
        else
        {
            string fullPath = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
            await File.WriteAllTextAsync(fullPath, content, cancellationToken);
            Console.WriteLine($"Отчёт сохранён: {fullPath}");
        }

        return 0;
    }

    private async Task<int> RunWatchAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var reader = new ArgumentReader(args);
        string profileName = reader.Get("profile", "standard");
        CaptureProfile profile = _profiles.Get(profileName);
        string session = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        string beforeName = $"watch-{session}-before";
        string afterName = $"watch-{session}-after";

        Console.WriteLine("1/4 Создание начального снимка…");
        SnapshotRecord before = await _coordinator.CaptureAsync(
            beforeName,
            profile,
            _paths.DataDirectory,
            IsAdministrator(),
            progress: null,
            cancellationToken);

        if (reader.Has("no-launch"))
        {
            Console.WriteLine();
            Console.WriteLine("Установите или запустите исследуемую программу.");
            Console.WriteLine("После завершения вернитесь в SysDiff и нажмите Enter.");
            Console.ReadLine();
        }
        else
        {
            string executable = RequirePosition(reader, 0, "Укажите исполняемый файл или --no-launch.");
            string expanded = Environment.ExpandEnvironmentVariables(executable);
            string workingDirectory = reader.Get(
                "working-directory",
                Path.GetDirectoryName(Path.GetFullPath(expanded)) ?? Environment.CurrentDirectory);

            var startInfo = new ProcessStartInfo
            {
                FileName = expanded,
                Arguments = reader.Get("arguments", string.Empty),
                WorkingDirectory = workingDirectory,
                UseShellExecute = true
            };

            Console.WriteLine($"2/4 Запуск: {expanded}");
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Не удалось запустить процесс.");
            await process.WaitForExitAsync(cancellationToken);
        }

        int delaySeconds = reader.GetInt("stabilization-delay", 3);
        if (delaySeconds > 0)
        {
            Console.WriteLine($"3/4 Ожидание стабилизации: {delaySeconds} сек.");
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
        }

        Console.WriteLine("4/4 Создание итогового снимка и сравнение…");
        SnapshotRecord after = await _coordinator.CaptureAsync(
            afterName,
            profile,
            _paths.DataDirectory,
            IsAdministrator(),
            progress: null,
            cancellationToken);

        ComparisonResult comparison =
            _comparisonEngine.Compare(before, after, NoiseMode.Balanced);
        await _store.SaveComparisonAsync(comparison, cancellationToken);

        string reportPath = reader.Get(
            "report",
            Path.Combine(_paths.ReportsDirectory, $"watch-{session}.html"));
        string html = _htmlRenderer.Render(before, after, comparison);
        string fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath) ?? ".");
        await File.WriteAllTextAsync(fullReportPath, html, cancellationToken);

        Console.WriteLine($"Готово. Найдено изменений: {comparison.Changes.Count:N0}");
        Console.WriteLine($"HTML-отчёт: {fullReportPath}");
        return before.Status == SnapshotStatus.Partial || after.Status == SnapshotStatus.Partial
            ? 7
            : 0;
    }

    private async Task<int> RunDoctorAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Диагностика SysDiff");
        Console.WriteLine(new string('─', 48));

        PrintCheck(OperatingSystem.IsWindows(), $"Windows: {Environment.OSVersion}");
        PrintCheck(Environment.Is64BitOperatingSystem, "Архитектура ОС: x64");
        PrintCheck(
            Environment.Version.Major >= 8,
            $".NET: {Environment.Version}");
        PrintCheck(IsAdministrator(), "Права администратора", warningWhenFalse: true);
        PrintCheck(Directory.Exists(_paths.DataDirectory), $"Каталог данных: {_paths.DataDirectory}");
        PrintCheck(_paths.Portable, "Portable-режим", warningWhenFalse: true);

        string probe = Path.Combine(_paths.DataDirectory, $".probe-{Guid.NewGuid():N}");
        try
        {
            await File.WriteAllTextAsync(probe, "ok", cancellationToken);
            File.Delete(probe);
            PrintCheck(true, "Запись в каталог данных");
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException)
        {
            PrintCheck(false, $"Запись в каталог данных: {exception.Message}");
        }

        try
        {
            await _store.InitializeAsync(cancellationToken);
            PrintCheck(true, $"SQLite: {_paths.DatabasePath}");
        }
        catch (Exception exception)
        {
            PrintCheck(false, $"SQLite: {exception.Message}");
            return 9;
        }

        return 0;
    }

    private int RunProfile(string[] args)
    {
        string action = args.Length > 0 ? args[0].ToLowerInvariant() : "list";

        if (action == "list")
        {
            foreach (CaptureProfile profile in _profiles.All.OrderBy(x => x.Name))
            {
                Console.WriteLine($"{profile.Name,-12} {profile.Description}");
            }

            return 0;
        }

        if (action == "show" && args.Length > 1)
        {
            CaptureProfile profile = _profiles.Get(args[1]);
            Console.WriteLine($"{profile.Name}: {profile.Description}");

            foreach ((string provider, ProviderOptions options) in profile.Providers)
            {
                Console.WriteLine(
                    $"  - {provider}: {(options.Enabled ? "включён" : "выключен")}");
            }

            return 0;
        }

        throw new ArgumentException("Используйте profile list или profile show <name>.");
    }

    private int RunConfig(string[] args)
    {
        string action = args.Length > 0 ? args[0].ToLowerInvariant() : "show";

        if (action == "path")
        {
            Console.WriteLine(_paths.DataDirectory);
            return 0;
        }

        if (action == "show")
        {
            Console.WriteLine($"Режим: {(_paths.Portable ? "portable" : "user")}");
            Console.WriteLine($"Данные: {_paths.DataDirectory}");
            Console.WriteLine($"База: {_paths.DatabasePath}");
            Console.WriteLine($"Отчёты: {_paths.ReportsDirectory}");
            Console.WriteLine($"Логи: {_paths.LogsDirectory}");
            return 0;
        }

        throw new ArgumentException("Используйте config show или config path.");
    }

    private async Task<int> RunInteractiveAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("SYSDIFF");
            Console.WriteLine(new string('─', 42));
            Console.WriteLine("[1] Создать снимок системы");
            Console.WriteLine("[2] Сравнить снимки");
            Console.WriteLine("[3] Наблюдать за программой");
            Console.WriteLine("[4] Просмотреть снимки");
            Console.WriteLine("[5] Диагностика SysDiff");
            Console.WriteLine("[0] Выход");
            Console.WriteLine();
            Console.Write("Выберите действие: ");

            string? value = Console.ReadLine();
            Console.WriteLine();

            switch (value)
            {
                case "1":
                    Console.Write("Имя снимка: ");
                    string name = Console.ReadLine() ?? string.Empty;
                    Console.Write("Профиль [standard]: ");
                    string profile = Console.ReadLine() ?? string.Empty;
                    await CreateSnapshotAsync(
                        [name, "--profile", string.IsNullOrWhiteSpace(profile) ? "standard" : profile],
                        cancellationToken);
                    Pause();
                    break;
                case "2":
                    Console.Write("Начальный снимок: ");
                    string before = Console.ReadLine() ?? string.Empty;
                    Console.Write("Итоговый снимок: ");
                    string after = Console.ReadLine() ?? string.Empty;
                    await RunCompareAsync([before, after], cancellationToken);
                    Pause();
                    break;
                case "3":
                    Console.Write("Путь к программе или пусто для ручного режима: ");
                    string executable = Console.ReadLine() ?? string.Empty;
                    string[] watchArgs = string.IsNullOrWhiteSpace(executable)
                        ? ["--no-launch"]
                        : [executable];
                    await RunWatchAsync(watchArgs, cancellationToken);
                    Pause();
                    break;
                case "4":
                    await ListSnapshotsAsync(cancellationToken);
                    Pause();
                    break;
                case "5":
                    await RunDoctorAsync(cancellationToken);
                    Pause();
                    break;
                case "0":
                    return 0;
            }
        }
    }

    private static string RequirePosition(
        ArgumentReader reader,
        int index,
        string message)
    {
        if (reader.Positionals.Count <= index
            || string.IsNullOrWhiteSpace(reader.Positionals[index]))
        {
            throw new ArgumentException(message);
        }

        return reader.Positionals[index];
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

    private static string StatusMarker(ProviderStatus status) => status switch
    {
        ProviderStatus.Success => "OK",
        ProviderStatus.Partial => "WARN",
        ProviderStatus.Failed => "FAIL",
        ProviderStatus.Skipped => "SKIP",
        ProviderStatus.Cancelled => "CANCEL",
        _ => "?"
    };

    private static void PrintCheck(
        bool success,
        string text,
        bool warningWhenFalse = false)
    {
        string marker = success ? "OK" : warningWhenFalse ? "WARN" : "FAIL";
        Console.WriteLine($"[{marker}] {text}");
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Неизвестная команда: {command}");
        Console.Error.WriteLine("Запустите sysdiff --help.");
        return 2;
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Нажмите Enter для продолжения…");
        Console.ReadLine();
    }

    private static string Shorten(string value, int maximum) =>
        value.Length <= maximum ? value : value[..(maximum - 1)] + "…";

    private static string SanitizeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(x => invalid.Contains(x) ? '_' : x).ToArray());
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            SysDiff — сравнение состояния Windows до и после запуска программы.

            ИСПОЛЬЗОВАНИЕ
              sysdiff
              sysdiff doctor
              sysdiff snapshot create <name> [--profile minimal|standard|full]
              sysdiff snapshot list
              sysdiff snapshot show <name-or-id>
              sysdiff snapshot delete <name-or-id> [--yes]
              sysdiff compare <before> <after> [--noise Raw|Balanced|Strict]
                     [--severity Info|Low|Medium|High|Critical]
                     [--format console|json|html|markdown] [--output <file>]
              sysdiff watch <executable> [--arguments "..."] [--profile standard]
                     [--working-directory <dir>] [--stabilization-delay 3]
                     [--report <file>]
              sysdiff watch --no-launch [--profile standard]
              sysdiff profile list
              sysdiff profile show <name>
              sysdiff config show
              sysdiff config path

            КОДЫ ЗАВЕРШЕНИЯ
              0  Успех
              1  Общая ошибка
              2  Ошибка аргументов
              3  Снимок не найден
              5  Доступ запрещён
              7  Частичный снимок
              8  Отменено
              9  Ошибка хранилища

            SysDiff не является антивирусом и не утверждает, что найденное изменение вредоносно.
            """);
    }
}
