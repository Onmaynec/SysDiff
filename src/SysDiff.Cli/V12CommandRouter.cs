using System.Text.Json;
using System.Text.Json.Serialization;
using SysDiff.Core;

namespace SysDiff.Cli;

internal sealed class V12CommandRouter
{
    private readonly V11CommandRouter _v11;
    private readonly ScaleLabService _scale;

    public V12CommandRouter(
        V11CommandRouter v11,
        ScaleLabService scale)
    {
        _v11 = v11;
        _scale = scale;
    }

    public async Task<int> RunAsync(
        string[] args,
        CommandApp fallback,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            return await _v11.RunAsync(args, fallback, cancellationToken);
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

        if (args[0].Equals("scale", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("large", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("stream", StringComparison.OrdinalIgnoreCase))
        {
            return await RunScaleAsync(args, cancellationToken);
        }

        int result = await _v11.RunAsync(args, fallback, cancellationToken);
        if (args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
        }
        return result;
    }

    private async Task<int> RunScaleAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        string command = args.Length > 1 ? args[1].ToLowerInvariant() : "matrix";
        bool json = HasOption(args, "--json");
        IProgress<ScaleProgress>? progress = json ? null : CreateProgress();

        switch (command)
        {
            case "matrix":
            case "status":
            case "limits":
                WriteResult(new
                {
                    productVersion = ProductInfo.Version,
                    streamFormat = "SysDiff Artifact NDJSON v1",
                    identityOrder = "ordinal-ignore-case ascending",
                    maximumArtifacts = 10_000_000,
                    defaultBatchSize = 50_000,
                    benchmarkArtifacts = 1_000_000,
                    boundedState = "one sort batch or one before/after artifact pair",
                    commands = new[] { "synth", "sort", "compare", "benchmark" }
                }, json, PrintMatrix);
                return 0;

            case "synth":
            case "generate":
            {
                if (args.Length < 3)
                {
                    throw new ArgumentException(
                        "Использование: scale synth <output.ndjson> [--count N] [--variant before|after] [--change-every N]");
                }
                var options = new ScaleSyntheticOptions
                {
                    Count = GetInt(args, "--count", 1_000_000),
                    Variant = GetOptionValue(args, "--variant") ?? "before",
                    ChangeEvery = GetInt(args, "--change-every", 1_000)
                };
                ScaleSyntheticResult result = await _scale.GenerateSyntheticAsync(
                    args[2],
                    options,
                    progress,
                    cancellationToken);
                CompleteProgress(json);
                WriteResult(result, json, () => PrintSynthetic(result));
                return 0;
            }

            case "sort":
            case "normalize":
            {
                if (args.Length < 3)
                {
                    throw new ArgumentException(
                        "Использование: scale sort <input.ndjson> --output <sorted.ndjson> [--batch-size N]");
                }
                string output = GetRequiredOption(args, "--output");
                var options = new ScaleSortOptions
                {
                    BatchSize = GetInt(args, "--batch-size", 50_000),
                    ProgressInterval = GetInt(args, "--progress-interval", 100_000)
                };
                ScaleSortResult result = await _scale.SortAsync(
                    args[2],
                    output,
                    options,
                    progress,
                    cancellationToken);
                CompleteProgress(json);
                WriteResult(result, json, () => PrintSort(result));
                return 0;
            }

            case "compare":
            case "diff":
            {
                if (args.Length < 4)
                {
                    throw new ArgumentException(
                        "Использование: scale compare <before.ndjson> <after.ndjson> --output <changes.ndjson>");
                }
                string output = GetRequiredOption(args, "--output");
                var options = new ScaleCompareOptions
                {
                    ProgressInterval = GetInt(args, "--progress-interval", 100_000),
                    IncludeUnchanged = HasOption(args, "--include-unchanged")
                };
                ScaleComparisonResult result = await _scale.CompareAsync(
                    args[2],
                    args[3],
                    output,
                    options,
                    progress,
                    cancellationToken);
                CompleteProgress(json);
                WriteResult(result, json, () => PrintComparison(result));
                return 0;
            }

            case "benchmark":
            case "bench":
            {
                string outputDirectory = GetOptionValue(args, "--output-dir")
                    ?? Path.Combine("artifacts", "scale");
                var options = new ScaleBenchmarkOptions
                {
                    ArtifactCount = GetInt(args, "--artifacts", 1_000_000),
                    ChangeEvery = GetInt(args, "--change-every", 1_000),
                    BatchSize = GetInt(args, "--batch-size", 50_000),
                    MaxManagedMemoryMb = GetInt(args, "--max-managed-mb", 256),
                    MinimumThroughputArtifactsPerSecond = GetDouble(
                        args,
                        "--min-throughput",
                        1_000)
                };
                ScaleBenchmarkResult result = await _scale.RunBenchmarkAsync(
                    outputDirectory,
                    options,
                    progress,
                    cancellationToken);
                CompleteProgress(json);
                WriteResult(result, json, () => PrintBenchmark(result));
                return result.Passed ? 0 : 10;
            }

            default:
                throw new ArgumentException(
                    "Команда scale: matrix, synth, sort, compare или benchmark.");
        }
    }

    private static IProgress<ScaleProgress> CreateProgress() =>
        new Progress<ScaleProgress>(value =>
        {
            Console.Error.Write(
                $"\rprocessed={value.Processed:N0} written={value.Written:N0} " +
                $"managed={ToMiB(value.ManagedBytes):N1} MiB " +
                $"working={ToMiB(value.WorkingSetBytes):N1} MiB      ");
        });

    private static void CompleteProgress(bool json)
    {
        if (!json)
        {
            Console.Error.WriteLine();
        }
    }

    private static void PrintMatrix()
    {
        Console.WriteLine("SysDiff Scale Lab");
        Console.WriteLine($"Product: {ProductInfo.Version}");
        Console.WriteLine("Stream: SysDiff Artifact NDJSON v1");
        Console.WriteLine("Order: identity, OrdinalIgnoreCase ascending");
        Console.WriteLine("Default batch: 50,000 lines");
        Console.WriteLine("Benchmark gate: 1,000,000 artifacts");
        Console.WriteLine("Bounded state: one sort batch or one before/after artifact pair");
    }

    private static void PrintSynthetic(ScaleSyntheticResult result)
    {
        Console.WriteLine("Synthetic scale dataset created");
        Console.WriteLine($"Output: {result.OutputPath}");
        Console.WriteLine($"Variant: {result.Variant}");
        Console.WriteLine($"Artifacts: {result.ArtifactCount:N0}");
        Console.WriteLine($"Size: {ToMiB(result.SizeBytes):N1} MiB");
        Console.WriteLine($"Duration: {result.Duration}");
    }

    private static void PrintSort(ScaleSortResult result)
    {
        Console.WriteLine("External sort completed");
        Console.WriteLine($"Input: {result.InputPath}");
        Console.WriteLine($"Output: {result.OutputPath}");
        Console.WriteLine($"Artifacts: {result.ArtifactCount:N0}");
        Console.WriteLine($"Chunks: {result.ChunkCount:N0}");
        Console.WriteLine($"Peak managed: {ToMiB(result.PeakManagedBytes):N1} MiB");
        Console.WriteLine($"Duration: {result.Duration}");
    }

    private static void PrintComparison(ScaleComparisonResult result)
    {
        Console.WriteLine("Streaming comparison completed");
        Console.WriteLine($"Output: {result.OutputPath}");
        Console.WriteLine($"Before/after: {result.BeforeArtifacts:N0} / {result.AfterArtifacts:N0}");
        Console.WriteLine(
            $"Added={result.Added:N0} Removed={result.Removed:N0} " +
            $"Modified={result.Modified:N0} Unchanged={result.Unchanged:N0}");
        Console.WriteLine($"Written changes: {result.WrittenChanges:N0}");
        Console.WriteLine($"Peak managed: {ToMiB(result.PeakManagedBytes):N1} MiB");
        Console.WriteLine($"Peak working set: {ToMiB(result.PeakWorkingSetBytes):N1} MiB");
        Console.WriteLine($"Throughput: {result.ThroughputArtifactsPerSecond:N0} artifacts/sec");
        Console.WriteLine($"Duration: {result.Duration}");
    }

    private static void PrintBenchmark(ScaleBenchmarkResult result)
    {
        Console.WriteLine(result.Passed ? "Scale benchmark PASSED" : "Scale benchmark FAILED");
        Console.WriteLine($"Result: {result.ResultPath}");
        Console.WriteLine($"Artifacts: {result.ArtifactCount:N0}");
        Console.WriteLine($"Modified: {result.ActualModified:N0} / {result.ExpectedModified:N0}");
        Console.WriteLine(
            $"Managed memory: {ToMiB(result.PeakManagedBytes):N1} / " +
            $"{result.MaxManagedMemoryMb:N0} MiB ({Gate(result.MemoryPassed)})");
        Console.WriteLine(
            $"Throughput: {result.ThroughputArtifactsPerSecond:N0} / " +
            $"{result.MinimumThroughputArtifactsPerSecond:N0} artifacts/sec " +
            $"({Gate(result.ThroughputPassed)})");
        Console.WriteLine($"Count gate: {Gate(result.CountPassed)}");
        Console.WriteLine($"Duration: {result.Duration}");
    }

    private static string Gate(bool passed) => passed ? "PASS" : "FAIL";

    private static double ToMiB(long bytes) => bytes / 1024d / 1024d;

    private static bool HasOption(IReadOnlyList<string> args, string option) =>
        args.Any(value => value.Equals(option, StringComparison.OrdinalIgnoreCase));

    private static string GetRequiredOption(IReadOnlyList<string> args, string option) =>
        GetOptionValue(args, option)
        ?? throw new ArgumentException($"Параметр {option} обязателен.");

    private static string? GetOptionValue(IReadOnlyList<string> args, string option)
    {
        for (int index = 0; index < args.Count; index++)
        {
            if (!args[index].Equals(option, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Параметр {option} требует значение.");
            }
            return args[index + 1];
        }
        return null;
    }

    private static int GetInt(IReadOnlyList<string> args, string option, int fallback)
    {
        string? value = GetOptionValue(args, option);
        return value is null
            ? fallback
            : int.TryParse(value, out int parsed)
                ? parsed
                : throw new ArgumentException($"Параметр {option} должен быть целым числом.");
    }

    private static double GetDouble(IReadOnlyList<string> args, string option, double fallback)
    {
        string? value = GetOptionValue(args, option);
        return value is null
            ? fallback
            : double.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double parsed)
                ? parsed
                : throw new ArgumentException($"Параметр {option} должен быть числом.");
    }

    private static void WriteResult<T>(T result, bool json, Action textWriter)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            textWriter();
        }
    }

    private static void PrintSmokeFrame()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("SYSDIFF CYBER CONSOLE 0.12.0 // SCALE LAB");
        Console.WriteLine("[09] SYSTEM NODE > EXTERNAL SORT | MERGE JOIN | NDJSON | BENCHMARK GATE");
        Console.WriteLine("TARGET: 1,000,000 | STATE: BOUNDED | REPORT: STREAM | REGRESSION: BLOCK");
        Console.WriteLine("================================================================================");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """

            SCALE LAB 0.12
              sysdiff scale matrix [--json]
              sysdiff scale synth <output.ndjson> [--count N] [--variant before|after] [--change-every N] [--json]
              sysdiff scale sort <input.ndjson> --output <sorted.ndjson> [--batch-size N] [--json]
              sysdiff scale compare <before.ndjson> <after.ndjson> --output <changes.ndjson> [--include-unchanged] [--json]
              sysdiff scale benchmark [--output-dir <dir>] [--artifacts 1000000]
                                      [--max-managed-mb 256] [--min-throughput 1000] [--json]

            Scale Lab использует NDJSON, external chunk sort и merge-join без materialize всех artifacts/changes.
            Input для compare должен быть отсортирован по identity; scale sort выполняет bounded-memory normalization.
            Benchmark возвращает exit code 10 при memory, throughput или count regression.
            """);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
