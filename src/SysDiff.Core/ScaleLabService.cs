using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SysDiff.Domain;

namespace SysDiff.Core;

public sealed class ScaleLabService
{
    private const int MaximumArtifactCount = 10_000_000;
    private const int MaximumLineChars = 4 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions PrettyJsonOptions = new(JsonOptions)
    {
        WriteIndented = true
    };

    public async Task<ScaleSyntheticResult> GenerateSyntheticAsync(
        string outputPath,
        ScaleSyntheticOptions options,
        IProgress<ScaleProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(options);
        ValidateCount(options.Count);
        if (options.ChangeEvery <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.ChangeEvery),
                "ChangeEvery должен быть больше нуля.");
        }

        string variant = options.Variant.Trim().ToLowerInvariant();
        if (variant is not ("before" or "after"))
        {
            throw new ArgumentException("Variant должен быть before или after.");
        }

        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
        string temporaryPath = TemporaryPath(fullPath);
        var stopwatch = Stopwatch.StartNew();
        long peakManaged = 0;
        long peakWorkingSet = 0;
        using Process process = Process.GetCurrentProcess();

        try
        {
            await using FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 1024,
                useAsync: true);
            await using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024 * 1024,
                leaveOpen: false);

            for (int index = 0; index < options.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool changed = variant == "after" && index % options.ChangeEvery == 0;
                var artifact = new SystemArtifact
                {
                    ProviderId = "synthetic.scale",
                    ArtifactType = "ScaleArtifact",
                    Identity = $"synthetic://artifact/{index:D9}",
                    DisplayName = $"Synthetic Artifact {index:D9}",
                    Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Sequence"] = ArtifactValue.From(index, "Int32"),
                        ["Bucket"] = ArtifactValue.From(index % 128, "Int32"),
                        ["Value"] = ArtifactValue.From(changed ? $"changed-{index}" : $"stable-{index}"),
                        ["Checksum"] = ArtifactValue.From($"{index:X8}-{(index * 31L):X12}")
                    },
                    Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "synthetic",
                        "scale-lab",
                        variant
                    }
                };

                await writer.WriteLineAsync(JsonSerializer.Serialize(artifact, JsonOptions));

                if ((index + 1) % 100_000 == 0 || index + 1 == options.Count)
                {
                    SampleMemory(process, ref peakManaged, ref peakWorkingSet);
                    progress?.Report(new ScaleProgress
                    {
                        Processed = index + 1,
                        Written = index + 1,
                        ManagedBytes = peakManaged,
                        WorkingSetBytes = peakWorkingSet
                    });
                }
            }

            await writer.FlushAsync(cancellationToken);
            AtomicReplace(temporaryPath, fullPath);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }

        stopwatch.Stop();
        return new ScaleSyntheticResult
        {
            OutputPath = fullPath,
            ArtifactCount = options.Count,
            SizeBytes = new FileInfo(fullPath).Length,
            Variant = variant,
            Duration = stopwatch.Elapsed
        };
    }

    public async Task<ScaleSortResult> SortAsync(
        string inputPath,
        string outputPath,
        ScaleSortOptions options,
        IProgress<ScaleProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(options);
        if (options.BatchSize is < 1_000 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.BatchSize),
                "BatchSize должен быть в диапазоне 1 000–1 000 000.");
        }

        string source = Path.GetFullPath(inputPath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("Scale input не найден.", source);
        }

        string output = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
        string workDirectory = Path.Combine(
            Path.GetDirectoryName(output) ?? ".",
            $".sysdiff-scale-sort-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDirectory);
        string temporaryOutput = TemporaryPath(output);
        var chunks = new List<string>();
        var batch = new List<SortLine>(options.BatchSize);
        var stopwatch = Stopwatch.StartNew();
        long processed = 0;
        long bytesRead = 0;
        long peakManaged = 0;
        long peakWorkingSet = 0;
        using Process process = Process.GetCurrentProcess();

        try
        {
            await using FileStream inputStream = new(
                source,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1024 * 1024,
                useAsync: true);
            using var reader = new StreamReader(
                inputStream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024 * 1024,
                leaveOpen: false);

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                ValidateLineLength(line);
                batch.Add(new SortLine(ExtractIdentity(line), line));
                processed++;
                bytesRead += Encoding.UTF8.GetByteCount(line) + 1;

                if (batch.Count >= options.BatchSize)
                {
                    chunks.Add(await WriteChunkAsync(
                        batch,
                        workDirectory,
                        chunks.Count,
                        cancellationToken));
                    batch.Clear();
                }

                if (processed % Math.Max(1, options.ProgressInterval) == 0)
                {
                    SampleMemory(process, ref peakManaged, ref peakWorkingSet);
                    progress?.Report(new ScaleProgress
                    {
                        Processed = processed,
                        Written = 0,
                        BytesRead = bytesRead,
                        ManagedBytes = peakManaged,
                        WorkingSetBytes = peakWorkingSet
                    });
                }
            }

            if (batch.Count > 0)
            {
                chunks.Add(await WriteChunkAsync(
                    batch,
                    workDirectory,
                    chunks.Count,
                    cancellationToken));
                batch.Clear();
            }

            await MergeChunksAsync(
                chunks,
                temporaryOutput,
                progress,
                process,
                processed,
                peakManaged,
                peakWorkingSet,
                cancellationToken);
            AtomicReplace(temporaryOutput, output);
        }
        catch
        {
            TryDelete(temporaryOutput);
            throw;
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }

        stopwatch.Stop();
        SampleMemory(process, ref peakManaged, ref peakWorkingSet);
        return new ScaleSortResult
        {
            InputPath = source,
            OutputPath = output,
            ArtifactCount = processed,
            ChunkCount = chunks.Count,
            PeakManagedBytes = peakManaged,
            PeakWorkingSetBytes = peakWorkingSet,
            Duration = stopwatch.Elapsed
        };
    }

    public async Task<ScaleComparisonResult> CompareAsync(
        string beforePath,
        string afterPath,
        string outputPath,
        ScaleCompareOptions options,
        IProgress<ScaleProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(beforePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(afterPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(options);

        string beforeFullPath = Path.GetFullPath(beforePath);
        string afterFullPath = Path.GetFullPath(afterPath);
        if (!File.Exists(beforeFullPath))
        {
            throw new FileNotFoundException("Before NDJSON не найден.", beforeFullPath);
        }
        if (!File.Exists(afterFullPath))
        {
            throw new FileNotFoundException("After NDJSON не найден.", afterFullPath);
        }

        string outputFullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath) ?? ".");
        string temporaryOutput = TemporaryPath(outputFullPath);
        var stopwatch = Stopwatch.StartNew();
        long compared = 0;
        long added = 0;
        long removed = 0;
        long modified = 0;
        long unchanged = 0;
        long written = 0;
        long peakManaged = 0;
        long peakWorkingSet = 0;
        using Process process = Process.GetCurrentProcess();

        try
        {
            await using var before = new ArtifactCursor(beforeFullPath);
            await using var after = new ArtifactCursor(afterFullPath);
            bool hasBefore = await before.MoveNextAsync(cancellationToken);
            bool hasAfter = await after.MoveNextAsync(cancellationToken);

            await using FileStream outputStream = new(
                temporaryOutput,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 1024,
                useAsync: true);
            await using var writer = new StreamWriter(
                outputStream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024 * 1024,
                leaveOpen: false);

            while (hasBefore || hasAfter)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ScaleChangeRecord? change;

                if (!hasBefore)
                {
                    change = CreateChange(ChangeType.Added, null, after.Current!);
                    added++;
                    hasAfter = await after.MoveNextAsync(cancellationToken);
                }
                else if (!hasAfter)
                {
                    change = CreateChange(ChangeType.Removed, before.Current!, null);
                    removed++;
                    hasBefore = await before.MoveNextAsync(cancellationToken);
                }
                else
                {
                    int comparison = StringComparer.OrdinalIgnoreCase.Compare(
                        before.Current!.Identity,
                        after.Current!.Identity);
                    if (comparison < 0)
                    {
                        change = CreateChange(ChangeType.Removed, before.Current, null);
                        removed++;
                        hasBefore = await before.MoveNextAsync(cancellationToken);
                    }
                    else if (comparison > 0)
                    {
                        change = CreateChange(ChangeType.Added, null, after.Current);
                        added++;
                        hasAfter = await after.MoveNextAsync(cancellationToken);
                    }
                    else
                    {
                        List<PropertyChange> propertyChanges = CompareArtifacts(
                            before.Current,
                            after.Current);
                        if (propertyChanges.Count == 0)
                        {
                            unchanged++;
                            change = options.IncludeUnchanged
                                ? CreateChange(ChangeType.Unchanged, before.Current, after.Current)
                                : null;
                        }
                        else
                        {
                            modified++;
                            change = CreateChange(
                                ChangeType.Modified,
                                before.Current,
                                after.Current,
                                propertyChanges);
                        }
                        hasBefore = await before.MoveNextAsync(cancellationToken);
                        hasAfter = await after.MoveNextAsync(cancellationToken);
                    }
                }

                compared++;
                if (change is not null)
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(change, JsonOptions));
                    written++;
                }

                if (compared % Math.Max(1, options.ProgressInterval) == 0)
                {
                    SampleMemory(process, ref peakManaged, ref peakWorkingSet);
                    progress?.Report(new ScaleProgress
                    {
                        Processed = compared,
                        Written = written,
                        BytesRead = before.BytesRead + after.BytesRead,
                        ManagedBytes = peakManaged,
                        WorkingSetBytes = peakWorkingSet
                    });
                }
            }

            await writer.FlushAsync(cancellationToken);
            AtomicReplace(temporaryOutput, outputFullPath);

            stopwatch.Stop();
            SampleMemory(process, ref peakManaged, ref peakWorkingSet);
            double seconds = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
            return new ScaleComparisonResult
            {
                BeforePath = beforeFullPath,
                AfterPath = afterFullPath,
                OutputPath = outputFullPath,
                BeforeArtifacts = before.ArtifactCount,
                AfterArtifacts = after.ArtifactCount,
                ComparedIdentities = compared,
                Added = added,
                Removed = removed,
                Modified = modified,
                Unchanged = unchanged,
                WrittenChanges = written,
                PeakManagedBytes = peakManaged,
                PeakWorkingSetBytes = peakWorkingSet,
                ThroughputArtifactsPerSecond = compared / seconds,
                Duration = stopwatch.Elapsed
            };
        }
        catch
        {
            TryDelete(temporaryOutput);
            throw;
        }
    }

    public async Task<ScaleBenchmarkResult> RunBenchmarkAsync(
        string outputDirectory,
        ScaleBenchmarkOptions options,
        IProgress<ScaleProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(options);
        ValidateCount(options.ArtifactCount);
        if (options.MaxManagedMemoryMb < 32)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.MaxManagedMemoryMb),
                "Memory gate должен быть не меньше 32 МБ.");
        }
        if (options.MinimumThroughputArtifactsPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.MinimumThroughputArtifactsPerSecond));
        }

        string directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);
        string beforePath = Path.Combine(directory, "scale-before.ndjson");
        string afterPath = Path.Combine(directory, "scale-after.ndjson");
        string changesPath = Path.Combine(directory, "scale-changes.ndjson");
        string resultPath = Path.Combine(directory, "scale-benchmark.json");
        var stopwatch = Stopwatch.StartNew();

        await GenerateSyntheticAsync(
            beforePath,
            new ScaleSyntheticOptions
            {
                Count = options.ArtifactCount,
                Variant = "before",
                ChangeEvery = options.ChangeEvery
            },
            progress,
            cancellationToken);
        await GenerateSyntheticAsync(
            afterPath,
            new ScaleSyntheticOptions
            {
                Count = options.ArtifactCount,
                Variant = "after",
                ChangeEvery = options.ChangeEvery
            },
            progress,
            cancellationToken);

        ScaleComparisonResult comparison = await CompareAsync(
            beforePath,
            afterPath,
            changesPath,
            new ScaleCompareOptions(),
            progress,
            cancellationToken);

        int expectedModified = ((options.ArtifactCount - 1) / options.ChangeEvery) + 1;
        long memoryLimitBytes = options.MaxManagedMemoryMb * 1024L * 1024L;
        var result = new ScaleBenchmarkResult
        {
            ProductVersion = SysDiffProduct.Version,
            OutputDirectory = directory,
            ResultPath = resultPath,
            ArtifactCount = options.ArtifactCount,
            ExpectedModified = expectedModified,
            ActualModified = comparison.Modified,
            PeakManagedBytes = comparison.PeakManagedBytes,
            PeakWorkingSetBytes = comparison.PeakWorkingSetBytes,
            ThroughputArtifactsPerSecond = comparison.ThroughputArtifactsPerSecond,
            MaxManagedMemoryMb = options.MaxManagedMemoryMb,
            MinimumThroughputArtifactsPerSecond = options.MinimumThroughputArtifactsPerSecond,
            MemoryPassed = comparison.PeakManagedBytes <= memoryLimitBytes,
            ThroughputPassed = comparison.ThroughputArtifactsPerSecond
                >= options.MinimumThroughputArtifactsPerSecond,
            CountPassed = comparison.Modified == expectedModified
                && comparison.Added == 0
                && comparison.Removed == 0,
            Duration = stopwatch.Elapsed
        };

        await File.WriteAllTextAsync(
            resultPath,
            JsonSerializer.Serialize(result, PrettyJsonOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        return result;
    }

    private static async Task<string> WriteChunkAsync(
        List<SortLine> batch,
        string workDirectory,
        int index,
        CancellationToken cancellationToken)
    {
        batch.Sort(static (left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.Identity, right.Identity));

        for (int item = 1; item < batch.Count; item++)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(
                batch[item - 1].Identity,
                batch[item].Identity))
            {
                throw new InvalidDataException(
                    $"Duplicate identity в scale input: {batch[item].Identity}");
            }
        }

        string path = Path.Combine(workDirectory, $"chunk-{index:D5}.ndjson");
        await using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024,
            useAsync: true);
        await using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024 * 1024,
            leaveOpen: false);
        foreach (SortLine item in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(item.Line);
        }
        await writer.FlushAsync(cancellationToken);
        return path;
    }

    private static async Task MergeChunksAsync(
        IReadOnlyList<string> chunks,
        string outputPath,
        IProgress<ScaleProgress>? progress,
        Process process,
        long total,
        long peakManaged,
        long peakWorkingSet,
        CancellationToken cancellationToken)
    {
        await using FileStream outputStream = new(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024,
            useAsync: true);
        await using var writer = new StreamWriter(
            outputStream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024 * 1024,
            leaveOpen: false);

        if (chunks.Count == 0)
        {
            await writer.FlushAsync(cancellationToken);
            return;
        }

        var cursors = new List<ChunkCursor>(chunks.Count);
        var queue = new PriorityQueue<int, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            for (int index = 0; index < chunks.Count; index++)
            {
                var cursor = new ChunkCursor(chunks[index]);
                cursors.Add(cursor);
                if (await cursor.MoveNextAsync(cancellationToken))
                {
                    queue.Enqueue(index, cursor.Identity!);
                }
            }

            long written = 0;
            string? previousIdentity = null;
            while (queue.TryDequeue(out int cursorIndex, out _))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ChunkCursor cursor = cursors[cursorIndex];
                if (previousIdentity is not null
                    && StringComparer.OrdinalIgnoreCase.Equals(previousIdentity, cursor.Identity))
                {
                    throw new InvalidDataException(
                        $"Duplicate identity в scale input: {cursor.Identity}");
                }

                await writer.WriteLineAsync(cursor.Line);
                previousIdentity = cursor.Identity;
                written++;
                if (await cursor.MoveNextAsync(cancellationToken))
                {
                    queue.Enqueue(cursorIndex, cursor.Identity!);
                }

                if (written % 100_000 == 0 || written == total)
                {
                    SampleMemory(process, ref peakManaged, ref peakWorkingSet);
                    progress?.Report(new ScaleProgress
                    {
                        Processed = total,
                        Written = written,
                        ManagedBytes = peakManaged,
                        WorkingSetBytes = peakWorkingSet
                    });
                }
            }

            await writer.FlushAsync(cancellationToken);
        }
        finally
        {
            foreach (ChunkCursor cursor in cursors)
            {
                await cursor.DisposeAsync();
            }
        }
    }

    private static ScaleChangeRecord CreateChange(
        ChangeType type,
        SystemArtifact? before,
        SystemArtifact? after,
        List<PropertyChange>? propertyChanges = null)
    {
        SystemArtifact artifact = after ?? before
            ?? throw new InvalidOperationException("Scale change не содержит artifact.");
        return new ScaleChangeRecord
        {
            ChangeType = type,
            Identity = artifact.Identity,
            ProviderId = artifact.ProviderId,
            ArtifactType = artifact.ArtifactType,
            DisplayName = artifact.DisplayName,
            Before = before,
            After = after,
            ChangedProperties = propertyChanges ?? []
        };
    }

    private static List<PropertyChange> CompareArtifacts(
        SystemArtifact before,
        SystemArtifact after)
    {
        var changes = new List<PropertyChange>();
        AddTextChange(changes, "ProviderId", before.ProviderId, after.ProviderId);
        AddTextChange(changes, "ArtifactType", before.ArtifactType, after.ArtifactType);
        AddTextChange(changes, "DisplayName", before.DisplayName, after.DisplayName);

        string beforeTags = string.Join(",", before.Tags.Order(StringComparer.OrdinalIgnoreCase));
        string afterTags = string.Join(",", after.Tags.Order(StringComparer.OrdinalIgnoreCase));
        AddTextChange(changes, "Tags", beforeTags, afterTags);

        foreach (string key in before.Properties.Keys.Union(
            after.Properties.Keys,
            StringComparer.OrdinalIgnoreCase))
        {
            before.Properties.TryGetValue(key, out ArtifactValue? oldValue);
            after.Properties.TryGetValue(key, out ArtifactValue? newValue);
            if (!ArtifactValuesEqual(oldValue, newValue))
            {
                changes.Add(new PropertyChange
                {
                    Name = key,
                    Before = oldValue,
                    After = newValue
                });
            }
        }
        return changes;
    }

    private static void AddTextChange(
        List<PropertyChange> changes,
        string name,
        string before,
        string after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            changes.Add(new PropertyChange
            {
                Name = name,
                Before = ArtifactValue.From(before),
                After = ArtifactValue.From(after)
            });
        }
    }

    private static bool ArtifactValuesEqual(ArtifactValue? left, ArtifactValue? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }
        if (left is null || right is null)
        {
            return false;
        }
        return string.Equals(left.Value, right.Value, StringComparison.Ordinal)
            && string.Equals(left.Type, right.Type, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Hash, right.Hash, StringComparison.OrdinalIgnoreCase)
            && left.Redacted == right.Redacted;
    }

    private static string ExtractIdentity(string line)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Каждая NDJSON line должна быть JSON object.");
            }
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("identity", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    return property.Value.GetString()!;
                }
            }
            throw new InvalidDataException("Scale artifact не содержит identity.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Scale NDJSON содержит повреждённую JSON line.", exception);
        }
    }

    private static void ValidateCount(int count)
    {
        if (count is < 1 or > MaximumArtifactCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                $"Количество artifacts должно быть в диапазоне 1–{MaximumArtifactCount:N0}.");
        }
    }

    private static void ValidateLineLength(string line)
    {
        if (line.Length > MaximumLineChars)
        {
            throw new InvalidDataException(
                $"NDJSON line превышает limit {MaximumLineChars:N0} chars.");
        }
    }

    private static void SampleMemory(
        Process process,
        ref long peakManaged,
        ref long peakWorkingSet)
    {
        long managed = GC.GetTotalMemory(forceFullCollection: false);
        process.Refresh();
        long workingSet = process.WorkingSet64;
        peakManaged = Math.Max(peakManaged, managed);
        peakWorkingSet = Math.Max(peakWorkingSet, workingSet);
    }

    private static string TemporaryPath(string outputPath) =>
        $"{outputPath}.tmp-{Guid.NewGuid():N}";

    private static void AtomicReplace(string temporaryPath, string outputPath)
    {
        if (File.Exists(outputPath))
        {
            File.Move(temporaryPath, outputPath, overwrite: true);
        }
        else
        {
            File.Move(temporaryPath, outputPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed record SortLine(string Identity, string Line);

    private sealed class ChunkCursor : IAsyncDisposable
    {
        private readonly StreamReader _reader;

        public ChunkCursor(string path)
        {
            _reader = new StreamReader(
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 64 * 1024,
                    useAsync: true),
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 64 * 1024,
                leaveOpen: false);
        }

        public string? Identity { get; private set; }

        public string Line { get; private set; } = string.Empty;

        public async Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            while (await _reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                ValidateLineLength(line);
                Line = line;
                Identity = ExtractIdentity(line);
                return true;
            }
            Line = string.Empty;
            Identity = null;
            return false;
        }

        public ValueTask DisposeAsync() => _reader.DisposeAsync();
    }

    private sealed class ArtifactCursor : IAsyncDisposable
    {
        private readonly StreamReader _reader;
        private string? _previousIdentity;

        public ArtifactCursor(string path)
        {
            _reader = new StreamReader(
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 1024 * 1024,
                    useAsync: true),
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024 * 1024,
                leaveOpen: false);
        }

        public SystemArtifact? Current { get; private set; }

        public long ArtifactCount { get; private set; }

        public long BytesRead { get; private set; }

        public async Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            while (await _reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                ValidateLineLength(line);
                SystemArtifact artifact;
                try
                {
                    artifact = JsonSerializer.Deserialize<SystemArtifact>(line, JsonOptions)
                        ?? throw new InvalidDataException("Scale artifact равен null.");
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException("Scale NDJSON содержит invalid artifact.", exception);
                }
                if (string.IsNullOrWhiteSpace(artifact.Identity))
                {
                    throw new InvalidDataException("Scale artifact не содержит identity.");
                }
                if (_previousIdentity is not null)
                {
                    int order = StringComparer.OrdinalIgnoreCase.Compare(
                        _previousIdentity,
                        artifact.Identity);
                    if (order == 0)
                    {
                        throw new InvalidDataException(
                            $"Duplicate identity: {artifact.Identity}");
                    }
                    if (order > 0)
                    {
                        throw new InvalidDataException(
                            $"Input не отсортирован: {_previousIdentity} > {artifact.Identity}. Выполните scale sort.");
                    }
                }
                _previousIdentity = artifact.Identity;
                Current = artifact;
                ArtifactCount++;
                BytesRead += Encoding.UTF8.GetByteCount(line) + 1;
                return true;
            }
            Current = null;
            return false;
        }

        public ValueTask DisposeAsync() => _reader.DisposeAsync();
    }
}
