using System.Security.Principal;
using SysDiff.Core;
using SysDiff.Domain;
using SysDiff.Reporting;
using SysDiff.Storage;

namespace SysDiff.Cli;

internal sealed class V3CommandRouter
{
    private readonly AppPaths _paths;
    private readonly ISnapshotStore _store;
    private readonly SnapshotCoordinator _coordinator;
    private readonly IReadOnlyCollection<ISnapshotProvider> _providers;
    private readonly ProfileLoader _profileLoader;
    private readonly SnapshotArchiveService _snapshotArchive;
    private readonly InvestigationBundleService _bundleService;
    private readonly ProcessLiveMonitor _processMonitor;
    private readonly NetworkLiveMonitor _networkMonitor;
    private readonly ComparisonEngine _comparisonEngine;
    private readonly ConsoleReportRenderer _consoleRenderer;
    private readonly JsonReportRenderer _jsonRenderer;
    private readonly MarkdownReportRenderer _markdownRenderer;
    private readonly HtmlReportRenderer _htmlRenderer;

    public V3CommandRouter(
        AppPaths paths,
        ISnapshotStore store,
        SnapshotCoordinator coordinator,
        IEnumerable<ISnapshotProvider> providers,
        ProfileLoader profileLoader,
        SnapshotArchiveService snapshotArchive,
        InvestigationBundleService bundleService,
        ProcessLiveMonitor processMonitor,
        NetworkLiveMonitor networkMonitor,
        ComparisonEngine comparisonEngine,
        ConsoleReportRenderer consoleRenderer,
        JsonReportRenderer jsonRenderer,
        MarkdownReportRenderer markdownRenderer,
        HtmlReportRenderer htmlRenderer)
    {
        _paths = paths;
        _store = store;
        _coordinator = coordinator;
        _providers = providers.ToArray();
        _profileLoader = profileLoader;
        _snapshotArchive = snapshotArchive;
        _bundleService = bundleService;
        _processMonitor = processMonitor;
        _networkMonitor = networkMonitor;
        _comparisonEngine = comparisonEngine;
        _consoleRenderer = consoleRenderer;
        _jsonRenderer = jsonRenderer;
        _markdownRenderer = markdownRenderer;
        _htmlRenderer = htmlRenderer;
    }

    public async Task<int> RunAsync(
        string[] args,
        CommandApp fallback,
        CancellationToken cancellationToken)
    {
        if (args.Length > 0 && args[0] is "--version" or "-v")
        {
            Console.WriteLine("SysDiff 0.3.0");
            return 0;
        }

        if (args.Length > 0 && args[0] is "--help" or "-h" or "help")
        {
            int result = await fallback.RunAsync(args, cancellationToken);
            PrintV3Help();
            return result;
        }

        if (args.Length == 0)
        {
            return await fallback.RunAsync(args, cancellationToken);
        }

        if (args[0].Equals("live", StringComparison.OrdinalIgnoreCase))
        {
            return await RunLiveAsync(args[1..], cancellationToken);
        }

        if (args[0].Equals("bundle", StringComparison.OrdinalIgnoreCase))
        {
            return await RunBundleAsync(args[1..], cancellationToken);
        }

        if (args[0].Equals("snapshot", StringComparison.OrdinalIgnoreCase)
            && args.Length > 1)
        {
            if (args[1].Equals("export", StringComparison.OrdinalIgnoreCase))
            {
                return await ExportSnapshotAsync(args[2..], cancellationToken);
            }

            if (args[1].Equals("import", StringComparison.OrdinalIgnoreCase))
            {
                return await ImportSnapshotAsync(args[2..], cancellationToken);
            }

            if (args[1].Equals("create", StringComparison.OrdinalIgnoreCase)
                && args.Any(x => x.StartsWith("--profile-file", StringComparison.OrdinalIgnoreCase)))
            {
                return await CreateSnapshotFromProfileAsync(args[2..], cancellationToken);
            }
        }

        if (args[0].Equals("profile", StringComparison.OrdinalIgnoreCase)
            && args.Length > 1
            && args[1].Equals("load", StringComparison.OrdinalIgnoreCase))
        {
            return await LoadProfileAsync(args[2..], cancellationToken);
        }

        if (args[0].Equals("compare", StringComparison.OrdinalIgnoreCase)
            && args.Any(x => x.Equals("--cross-machine", StringComparison.OrdinalIgnoreCase)))
        {
            return await CompareCrossMachineAsync(args[1..], cancellationToken);
        }

        return await fallback.RunAsync(args, cancellationToken);
    }

    private async Task<int> RunLiveAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("Используйте live process или live network.");
        }

        string category = args[0].ToLowerInvariant();
        var reader = new ArgumentReader(args[1..]);
        int durationSeconds = Math.Clamp(reader.GetInt("duration", 30), 1, 86_400);
        string format = reader.Get("format", "json");
        IReadOnlyList<LiveEvent> events = category switch
        {
            "process" => await _processMonitor.MonitorAsync(
                TimeSpan.FromSeconds(durationSeconds),
                reader.Get("root-pid") is { Length: > 0 } rootText
                    && int.TryParse(rootText, out int rootPid)
                    ? rootPid
                    : null,
                cancellationToken),
            "network" => await _networkMonitor.MonitorAsync(
                TimeSpan.FromSeconds(durationSeconds),
                cancellationToken),
            _ => throw new ArgumentException("Категория live должна быть process или network.")
        };

        string content = LiveEventWriter.Render(events, format);
        string extension = format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            || format.Equals("md", StringComparison.OrdinalIgnoreCase)
            ? "md"
            : "json";
        string output = reader.Get(
            "output",
            Path.Combine(
                _paths.ReportsDirectory,
                $"live-{category}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.{extension}"));
        string fullPath = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
        Console.WriteLine($"Событий: {events.Count:N0}");
        Console.WriteLine($"Журнал: {fullPath}");
        return 0;
    }

    private async Task<int> RunBundleAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || !args[0].Equals("create", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Используйте bundle create <comparison-id>.");
        }

        var reader = new ArgumentReader(args[1..]);
        if (reader.Positionals.Count == 0
            || !Guid.TryParse(reader.Positionals[0], out Guid comparisonId))
        {
            throw new ArgumentException("Укажите корректный comparison ID.");
        }

        string output = reader.Get(
            "output",
            Path.Combine(_paths.ReportsDirectory, $"investigation-{comparisonId:N}.zip"));
        string path = await _bundleService.CreateAsync(comparisonId, output, cancellationToken);
        Console.WriteLine($"Investigation bundle: {path}");
        return 0;
    }

    private async Task<int> ExportSnapshotAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var reader = new ArgumentReader(args);
        if (reader.Positionals.Count == 0)
        {
            throw new ArgumentException("Укажите имя или ID снимка.");
        }

        SnapshotRecord snapshot = await _store.GetSnapshotAsync(
            reader.Positionals[0],
            cancellationToken)
            ?? throw new InvalidOperationException("Снимок не найден.");
        string output = reader.Get(
            "output",
            Path.Combine(_paths.ReportsDirectory, $"{Sanitize(snapshot.Name)}.sdshot"));
        string path = await _snapshotArchive.ExportAsync(snapshot, output, cancellationToken);
        Console.WriteLine($"Снимок экспортирован: {path}");
        return 0;
    }

    private async Task<int> ImportSnapshotAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var reader = new ArgumentReader(args);
        if (reader.Positionals.Count == 0)
        {
            throw new ArgumentException("Укажите файл .sdshot.");
        }

        SnapshotRecord snapshot = await _snapshotArchive.ImportAsync(
            reader.Positionals[0],
            cancellationToken);
        Console.WriteLine($"Снимок импортирован: {snapshot.Name} ({snapshot.Id})");
        return 0;
    }

    private async Task<int> CreateSnapshotFromProfileAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var reader = new ArgumentReader(args);
        if (reader.Positionals.Count == 0)
        {
            throw new ArgumentException("Укажите имя снимка.");
        }

        string profilePath = reader.Get("profile-file")
            ?? throw new ArgumentException("Укажите --profile-file <file>.");
        CaptureProfile profile = await _profileLoader.LoadAsync(
            profilePath,
            _providers.Select(x => x.Id).ToArray(),
            cancellationToken);
        SnapshotRecord snapshot = await _coordinator.CaptureAsync(
            reader.Positionals[0],
            profile,
            _paths.DataDirectory,
            IsAdministrator(),
            progress: null,
            cancellationToken);
        Console.WriteLine(
            $"Снимок сохранён: {snapshot.Id} · {snapshot.Artifacts.Count:N0} объектов · {snapshot.Status}");
        return snapshot.Status == SnapshotStatus.Partial ? 7 : 0;
    }

    private async Task<int> LoadProfileAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var reader = new ArgumentReader(args);
        if (reader.Positionals.Count == 0)
        {
            throw new ArgumentException("Укажите JSON-файл профиля.");
        }

        CaptureProfile profile = await _profileLoader.LoadAsync(
            reader.Positionals[0],
            _providers.Select(x => x.Id).ToArray(),
            cancellationToken);
        Console.WriteLine($"Профиль: {profile.Name}");
        Console.WriteLine(profile.Description);
        foreach ((string id, ProviderOptions options) in profile.Providers)
        {
            Console.WriteLine($"- {id}: {(options.Enabled ? "enabled" : "disabled")}");
        }

        return 0;
    }

    private async Task<int> CompareCrossMachineAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var reader = new ArgumentReader(args);
        if (reader.Positionals.Count < 2)
        {
            throw new ArgumentException("Укажите начальный и итоговый снимки.");
        }

        SnapshotRecord before = await _store.GetSnapshotAsync(
            reader.Positionals[0],
            cancellationToken)
            ?? throw new InvalidOperationException("Начальный снимок не найден.");
        SnapshotRecord after = await _store.GetSnapshotAsync(
            reader.Positionals[1],
            cancellationToken)
            ?? throw new InvalidOperationException("Итоговый снимок не найден.");
        NoiseMode noiseMode = Enum.TryParse(
            reader.Get("noise", "Balanced"),
            ignoreCase: true,
            out NoiseMode parsed)
            ? parsed
            : throw new ArgumentException("Допустимые режимы шума: Raw, Balanced, Strict.");
        ComparisonResult comparison = _comparisonEngine.Compare(
            before,
            after,
            noiseMode,
            crossMachine: true);
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

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string Sanitize(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(x => invalid.Contains(x) ? '_' : x).ToArray());
    }

    private static void PrintV3Help()
    {
        Console.WriteLine(
            """

            ВОЗМОЖНОСТИ 0.3
              sysdiff live process [--duration 30] [--root-pid <pid>] [--format json|markdown]
              sysdiff live network [--duration 30] [--format json|markdown]
              sysdiff snapshot export <name-or-id> [--output file.sdshot]
              sysdiff snapshot import <file.sdshot>
              sysdiff snapshot create <name> --profile-file <profile.json>
              sysdiff profile load <profile.json>
              sysdiff compare <before> <after> --cross-machine
              sysdiff bundle create <comparison-id> [--output investigation.zip]
              sysdiff ... --plugin <provider.dll>
            """);
    }
}
