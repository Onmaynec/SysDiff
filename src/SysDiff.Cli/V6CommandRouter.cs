using SysDiff.Core;
using SysDiff.Domain;

namespace SysDiff.Cli;

internal sealed class V6CommandRouter
{
    private readonly V4CommandRouter _v4;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IInvestigationStore _investigationStore;
    private readonly DriftOperationsService _drift;
    private readonly ProfileCatalog _profiles;

    public V6CommandRouter(
        V4CommandRouter v4,
        ISnapshotStore snapshotStore,
        IInvestigationStore investigationStore,
        DriftOperationsService drift,
        ProfileCatalog profiles)
    {
        _v4 = v4;
        _snapshotStore = snapshotStore;
        _investigationStore = investigationStore;
        _drift = drift;
        _profiles = profiles;
    }

    public async Task<int> RunAsync(
        string[] args,
        CommandApp fallback,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            return await _v4.RunAsync(args, fallback, cancellationToken);
        }

        if (args[0] is "--version" or "-v")
        {
            Console.WriteLine("SysDiff 0.6.0");
            return 0;
        }

        if (args[0].Equals("--tui-smoke", StringComparison.OrdinalIgnoreCase))
        {
            PrintSmokeFrame();
            return 0;
        }

        if (args[0].Equals("baseline", StringComparison.OrdinalIgnoreCase))
        {
            return await RunBaselineAsync(args, cancellationToken);
        }

        if (args[0].Equals("drift", StringComparison.OrdinalIgnoreCase))
        {
            return await RunDriftAsync(args, cancellationToken);
        }

        if (args[0].Equals("timeline", StringComparison.OrdinalIgnoreCase))
        {
            return await RunTimelineAsync(args, cancellationToken);
        }

        if (args[0].Equals("case", StringComparison.OrdinalIgnoreCase))
        {
            return await RunCaseAsync(args, cancellationToken);
        }

        int result = await _v4.RunAsync(args, fallback, cancellationToken);
        if (args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
        }
        return result;
    }

    private async Task<int> RunBaselineAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        string command = args.Length > 1 ? args[1].ToLowerInvariant() : "show";
        switch (command)
        {
            case "show":
            {
                BaselineRecord? baseline =
                    await _investigationStore.GetBaselineAsync(cancellationToken);
                if (baseline is null)
                {
                    Console.WriteLine("Baseline не настроена.");
                    return 3;
                }
                Console.WriteLine($"Baseline: {baseline.SnapshotName}");
                Console.WriteLine($"Snapshot ID: {baseline.SnapshotId:D}");
                Console.WriteLine($"Установлена: {baseline.SetAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}");
                if (!string.IsNullOrWhiteSpace(baseline.Note))
                {
                    Console.WriteLine($"Заметка: {baseline.Note}");
                }
                return 0;
            }
            case "set":
            {
                if (args.Length < 3)
                {
                    throw new ArgumentException("Использование: baseline set <snapshot-name-or-id> [--note text]");
                }
                BaselineRecord baseline = await _drift.SetBaselineAsync(
                    args[2],
                    GetOption(args, "--note"),
                    cancellationToken);
                Console.WriteLine($"Baseline установлена: {baseline.SnapshotName} ({baseline.SnapshotId:D})");
                return 0;
            }
            case "clear":
                await _drift.ClearBaselineAsync(cancellationToken);
                Console.WriteLine("Baseline очищена.");
                return 0;
            default:
                throw new ArgumentException("Команда baseline: show, set или clear.");
        }
    }

    private async Task<int> RunDriftAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        if (args.Length < 2 || !args[1].Equals("scan", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Использование: drift scan [--profile standard] [--noise Balanced]");
        }

        string profileName = GetOption(args, "--profile") ?? "standard";
        string noiseText = GetOption(args, "--noise") ?? "Balanced";
        CaptureProfile profile = _profiles.Get(profileName);
        NoiseMode noise = Enum.Parse<NoiseMode>(noiseText, ignoreCase: true);
        IProgress<SnapshotProgress>? progress = Console.IsOutputRedirected
            ? null
            : new InlineProgress<SnapshotProgress>(value =>
            {
                string current = string.IsNullOrWhiteSpace(value.CurrentItem)
                    ? value.Message
                    : value.CurrentItem;
                Console.Write($"\r[{value.ProviderId}] {value.Processed:N0} · {Fit(current, 72)}");
            });

        DriftScanResult result = await _drift.ScanAsync(
            profile,
            noise,
            progress,
            cancellationToken);
        if (!Console.IsOutputRedirected)
        {
            Console.WriteLine();
        }
        PrintRisk(result);
        return result.CurrentSnapshot.Status == SnapshotStatus.Partial ? 7 : 0;
    }

    private async Task<int> RunTimelineAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        if (args.Length > 1 && !args[1].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Использование: timeline list [--limit 50] [--kind DriftScan]");
        }

        int limit = ParseIntOption(args, "--limit", 50, 1, 1000);
        TimelineEventKind? kind = null;
        string? kindText = GetOption(args, "--kind");
        if (!string.IsNullOrWhiteSpace(kindText))
        {
            kind = Enum.Parse<TimelineEventKind>(kindText, ignoreCase: true);
        }

        IReadOnlyList<TimelineEventRecord> events =
            await _investigationStore.ListTimelineAsync(limit, kind, cancellationToken);
        if (events.Count == 0)
        {
            Console.WriteLine("Timeline пока пуста.");
            return 0;
        }

        foreach (TimelineEventRecord item in events)
        {
            string severity = item.Severity is null ? "-" : item.Severity.Value.ToString();
            Console.WriteLine(
                $"{item.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}  {item.Kind,-11} {severity,-8} {item.Title}");
        }
        return 0;
    }

    private async Task<int> RunCaseAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("Команда case: create, list, show, use или close.");
        }

        switch (args[1].ToLowerInvariant())
        {
            case "create":
            {
                if (args.Length < 3)
                {
                    throw new ArgumentException("Использование: case create <name> [--description text] [--tags a,b]");
                }
                var investigationCase = new InvestigationCaseRecord
                {
                    Name = args[2],
                    Description = GetOption(args, "--description") ?? string.Empty,
                    Tags = ParseTags(GetOption(args, "--tags"))
                };
                InvestigationCaseRecord created = await _investigationStore.CreateCaseAsync(
                    investigationCase,
                    cancellationToken);
                await _investigationStore.SetActiveCaseAsync(created.Id, cancellationToken);
                Console.WriteLine($"Кейс создан и активирован: {created.Name} ({created.Id:D})");
                return 0;
            }
            case "list":
            {
                InvestigationCaseRecord? active =
                    await _investigationStore.GetActiveCaseAsync(cancellationToken);
                IReadOnlyList<InvestigationCaseRecord> cases =
                    await _investigationStore.ListCasesAsync(cancellationToken);
                if (cases.Count == 0)
                {
                    Console.WriteLine("Кейсов пока нет.");
                    return 0;
                }
                foreach (InvestigationCaseRecord item in cases)
                {
                    string marker = active?.Id == item.Id ? "*" : " ";
                    Console.WriteLine(
                        $"{marker} {item.Name,-28} {item.Status,-6} links:{item.Links.Count,3} updated:{item.UpdatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
                }
                return 0;
            }
            case "show":
            {
                if (args.Length < 3)
                {
                    throw new ArgumentException("Использование: case show <name-or-id>");
                }
                InvestigationCaseRecord item = await _investigationStore.GetCaseAsync(args[2], cancellationToken)
                    ?? throw new InvalidOperationException("Кейс не найден.");
                Console.WriteLine($"Кейс: {item.Name}");
                Console.WriteLine($"ID: {item.Id:D}");
                Console.WriteLine($"Статус: {item.Status}");
                Console.WriteLine($"Описание: {item.Description}");
                Console.WriteLine($"Теги: {(item.Tags.Count == 0 ? "-" : string.Join(", ", item.Tags))}");
                Console.WriteLine($"Связей: {item.Links.Count:N0}");
                foreach (InvestigationLink link in item.Links)
                {
                    Console.WriteLine($"  {link.Kind,-12} {link.DisplayName} [{link.ReferenceId}]");
                }
                return 0;
            }
            case "use":
            {
                if (args.Length < 3)
                {
                    throw new ArgumentException("Использование: case use <name-or-id|none>");
                }
                if (args[2].Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    await _investigationStore.SetActiveCaseAsync(null, cancellationToken);
                    Console.WriteLine("Активный кейс снят.");
                    return 0;
                }
                InvestigationCaseRecord item = await _investigationStore.GetCaseAsync(args[2], cancellationToken)
                    ?? throw new InvalidOperationException("Кейс не найден.");
                await _investigationStore.SetActiveCaseAsync(item.Id, cancellationToken);
                Console.WriteLine($"Активный кейс: {item.Name}");
                return 0;
            }
            case "close":
            {
                if (args.Length < 3)
                {
                    throw new ArgumentException("Использование: case close <name-or-id>");
                }
                InvestigationCaseRecord item = await _investigationStore.GetCaseAsync(args[2], cancellationToken)
                    ?? throw new InvalidOperationException("Кейс не найден.");
                await _investigationStore.UpdateCaseAsync(
                    item with { Status = InvestigationCaseStatus.Closed },
                    cancellationToken);
                InvestigationCaseRecord? active =
                    await _investigationStore.GetActiveCaseAsync(cancellationToken);
                if (active?.Id == item.Id)
                {
                    await _investigationStore.SetActiveCaseAsync(null, cancellationToken);
                }
                Console.WriteLine($"Кейс закрыт: {item.Name}");
                return 0;
            }
            default:
                throw new ArgumentException("Команда case: create, list, show, use или close.");
        }
    }

    private static void PrintRisk(DriftScanResult result)
    {
        Console.WriteLine("Drift Scan завершён");
        Console.WriteLine($"Baseline: {result.Baseline.SnapshotName}");
        Console.WriteLine($"Current: {result.CurrentSnapshot.Name} ({result.CurrentSnapshot.Status})");
        Console.WriteLine($"Risk: {result.Risk.Score}/100 · {result.Risk.Level}");
        Console.WriteLine($"Изменений: {result.Risk.TotalChanges:N0}");
        foreach (string factor in result.Risk.Factors)
        {
            Console.WriteLine($"- {factor}");
        }
        Console.WriteLine($"HTML: {result.HtmlReportPath}");
        Console.WriteLine($"JSON: {result.JsonReportPath}");
    }

    private static string? GetOption(IReadOnlyList<string> args, string option)
    {
        for (int index = 0; index < args.Count; index++)
        {
            if (args[index].Equals(option, StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Count)
                {
                    throw new ArgumentException($"Для {option} требуется значение.");
                }
                return args[index + 1];
            }
        }
        return null;
    }

    private static int ParseIntOption(
        IReadOnlyList<string> args,
        string option,
        int defaultValue,
        int minimum,
        int maximum)
    {
        string? text = GetOption(args, option);
        if (text is null)
        {
            return defaultValue;
        }
        if (!int.TryParse(text, out int value) || value < minimum || value > maximum)
        {
            throw new ArgumentException($"{option} должен быть числом от {minimum} до {maximum}.");
        }
        return value;
    }

    private static HashSet<string> ParseTags(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(
                value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase);

    private static string Fit(string? value, int width)
    {
        string text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
        return text.Length <= width ? text.PadRight(width) : text[..(width - 1)] + "…";
    }

    private static void PrintSmokeFrame()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("SYSDIFF CYBER CONSOLE 0.6.0 // DRIFT OPERATIONS");
        Console.WriteLine("[01] SNAPSHOT NODE  [02] DIFF LAB  [03] DRIFT OPS  [04] TIMELINE  [05] CASES");
        Console.WriteLine("BASELINE: READY | RISK: 00/100 | ACTIVE CASE: NONE | MOTION: SAFE");
        Console.WriteLine("================================================================================");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """

            DRIFT OPERATIONS 0.6
              sysdiff baseline show
              sysdiff baseline set <snapshot> [--note text]
              sysdiff baseline clear
              sysdiff drift scan [--profile standard] [--noise Balanced]
              sysdiff timeline list [--limit 50] [--kind DriftScan]
              sysdiff case create <name> [--description text] [--tags a,b]
              sysdiff case list
              sysdiff case show <name-or-id>
              sysdiff case use <name-or-id|none>
              sysdiff case close <name-or-id>
            """);
    }
}
