using System.Security.Cryptography;
using SysDiff.Domain;

namespace SysDiff.Providers;

public sealed class FileSystemProvider : ISnapshotProvider
{
    private const long SmartHashThreshold = 32L * 1024L * 1024L;

    public string Id => "filesystem";

    public string DisplayName => "Файловая система";

    public bool RequiresAdministrator => false;

    public async Task<ProviderSnapshotResult> CaptureAsync(
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var artifacts = new List<SystemArtifact>();
        var warnings = new List<string>();
        var errors = new List<string>();

        if (!context.Profile.Providers.TryGetValue(Id, out ProviderOptions? options))
        {
            return Skipped(started, "Провайдер отсутствует в профиле.");
        }

        foreach (string configuredRoot in options.Roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string root = ProviderUtilities.ExpandPath(configuredRoot);
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                warnings.Add($"Каталог недоступен: {root}");
                continue;
            }

            await ScanRootAsync(
                root,
                options,
                artifacts,
                warnings,
                context.Progress,
                cancellationToken);
        }

        ProviderStatus status = errors.Count > 0
            ? ProviderStatus.Partial
            : warnings.Count > 0
                ? ProviderStatus.Partial
                : ProviderStatus.Success;

        return new ProviderSnapshotResult
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            Status = status,
            StartedAtUtc = started,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            ArtifactCount = artifacts.Count,
            Artifacts = artifacts,
            Warnings = warnings,
            Errors = errors
        };
    }

    private static async Task ScanRootAsync(
        string root,
        ProviderOptions options,
        List<SystemArtifact> artifacts,
        List<string> warnings,
        IProgress<SnapshotProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stack = new Stack<(string Path, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        stack.Push((root, 0));

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (string current, int depth) = stack.Pop();
            string normalized;

            try
            {
                normalized = ProviderUtilities.NormalizePath(current);
            }
            catch (Exception exception) when (
                exception is IOException
                or UnauthorizedAccessException
                or NotSupportedException)
            {
                warnings.Add($"{current}: {exception.Message}");
                continue;
            }

            if (!visited.Add(normalized)
                || ProviderUtilities.MatchesAny(current, options.Exclude))
            {
                continue;
            }

            DirectoryInfo directory;
            try
            {
                directory = new DirectoryInfo(current);
                if (!directory.Exists)
                {
                    continue;
                }

                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    artifacts.Add(CreateDirectoryArtifact(directory, isReparsePoint: true));
                    continue;
                }

                artifacts.Add(CreateDirectoryArtifact(directory, isReparsePoint: false));
            }
            catch (Exception exception) when (
                exception is IOException
                or UnauthorizedAccessException
                or System.Security.SecurityException)
            {
                warnings.Add($"{current}: {exception.Message}");
                continue;
            }

            if (artifacts.Count >= options.MaximumArtifacts)
            {
                warnings.Add($"Достигнут лимит объектов: {options.MaximumArtifacts}.");
                return;
            }

            progress?.Report(new SnapshotProgress(
                "filesystem",
                "Сканирование файловой системы",
                artifacts.Count,
                current));

            FileInfo[] files;
            DirectoryInfo[] directories;

            try
            {
                files = directory.GetFiles();
                directories = depth < options.MaximumDepth
                    ? directory.GetDirectories()
                    : [];
            }
            catch (Exception exception) when (
                exception is IOException
                or UnauthorizedAccessException
                or System.Security.SecurityException)
            {
                warnings.Add($"{current}: {exception.Message}");
                continue;
            }

            foreach (FileInfo file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ProviderUtilities.MatchesAny(file.FullName, options.Exclude))
                {
                    continue;
                }

                try
                {
                    artifacts.Add(await CreateFileArtifactAsync(file, options, cancellationToken));
                }
                catch (Exception exception) when (
                    exception is IOException
                    or UnauthorizedAccessException
                    or System.Security.SecurityException)
                {
                    warnings.Add($"{file.FullName}: {exception.Message}");
                }

                if (artifacts.Count >= options.MaximumArtifacts)
                {
                    warnings.Add($"Достигнут лимит объектов: {options.MaximumArtifacts}.");
                    return;
                }
            }

            foreach (DirectoryInfo child in directories)
            {
                stack.Push((child.FullName, depth + 1));
            }
        }
    }

    private static SystemArtifact CreateDirectoryArtifact(
        DirectoryInfo directory,
        bool isReparsePoint) =>
        new()
        {
            ProviderId = "filesystem",
            ArtifactType = "Directory",
            Identity = ProviderUtilities.FileIdentity(directory.FullName),
            DisplayName = directory.FullName,
            Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["Path"] = ArtifactValue.From(directory.FullName),
                ["CreationTimeUtc"] = ArtifactValue.From(directory.CreationTimeUtc, "DateTime"),
                ["LastWriteTimeUtc"] = ArtifactValue.From(directory.LastWriteTimeUtc, "DateTime"),
                ["Attributes"] = ArtifactValue.From(directory.Attributes),
                ["IsReparsePoint"] = ArtifactValue.From(isReparsePoint, "Boolean"),
                ["LinkTarget"] = ArtifactValue.From(directory.LinkTarget)
            }
        };

    private static async Task<SystemArtifact> CreateFileArtifactAsync(
        FileInfo file,
        ProviderOptions options,
        CancellationToken cancellationToken)
    {
        string? hash = await ComputeHashIfRequiredAsync(file, options, cancellationToken);

        return new SystemArtifact
        {
            ProviderId = "filesystem",
            ArtifactType = "File",
            Identity = ProviderUtilities.FileIdentity(file.FullName),
            DisplayName = file.FullName,
            Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["Path"] = ArtifactValue.From(file.FullName),
                ["Size"] = ArtifactValue.From(file.Length, "Int64"),
                ["CreationTimeUtc"] = ArtifactValue.From(file.CreationTimeUtc, "DateTime"),
                ["LastWriteTimeUtc"] = ArtifactValue.From(file.LastWriteTimeUtc, "DateTime"),
                ["Attributes"] = ArtifactValue.From(file.Attributes),
                ["Extension"] = ArtifactValue.From(file.Extension),
                ["Sha256"] = ArtifactValue.From(hash),
                ["IsReparsePoint"] = ArtifactValue.From(
                    (file.Attributes & FileAttributes.ReparsePoint) != 0,
                    "Boolean"),
                ["LinkTarget"] = ArtifactValue.From(file.LinkTarget)
            }
        };
    }

    private static async Task<string?> ComputeHashIfRequiredAsync(
        FileInfo file,
        ProviderOptions options,
        CancellationToken cancellationToken)
    {
        if (options.HashMode is HashMode.None or HashMode.MetadataOnly)
        {
            return null;
        }

        if (file.Length > options.MaximumFileSizeBytes)
        {
            return null;
        }

        bool executable = file.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            || file.Extension.Equals(".sys", StringComparison.OrdinalIgnoreCase);

        if (options.HashMode == HashMode.Smart
            && !executable
            && file.Length > SmartHashThreshold)
        {
            return null;
        }

        await using FileStream stream = new(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            1024 * 128,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private ProviderSnapshotResult Skipped(DateTimeOffset started, string message) =>
        new()
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            Status = ProviderStatus.Skipped,
            StartedAtUtc = started,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            Warnings = [message]
        };
}
