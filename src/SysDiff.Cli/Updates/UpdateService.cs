using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SysDiff.Cli;

public sealed class UpdateService
{
    private readonly HttpClient _httpClient;
    private readonly AppPaths _paths;
    private readonly UpdateSettingsStore _store;

    public UpdateService(
        HttpClient httpClient,
        AppPaths paths,
        UpdateSettingsStore store)
    {
        _httpClient = httpClient;
        _paths = paths;
        _store = store;
    }

    public async Task<UpdateCheckResult> TryAutoCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await CheckAsync(force: false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException
            or IOException
            or InvalidDataException)
        {
            UpdateState previous = await _store.LoadStateAsync(CancellationToken.None);
            var failed = previous with
            {
                Status = UpdateStatus.Error,
                Error = exception.Message,
                LastCheckedAtUtc = DateTimeOffset.UtcNow
            };
            await _store.SaveStateAsync(failed, CancellationToken.None);
            return new UpdateCheckResult(
                UpdateStatus.Error,
                ProductInfo.Version,
                previous.Manifest,
                exception.Message);
        }
    }

    public async Task<UpdateCheckResult> CheckAsync(
        bool force,
        CancellationToken cancellationToken)
    {
        UpdateSettings settings = await _store.LoadSettingsAsync(cancellationToken);
        if (!force && !settings.AutoCheck)
        {
            return new UpdateCheckResult(
                UpdateStatus.Disabled,
                ProductInfo.Version,
                null,
                "Автоматическая проверка обновлений отключена.");
        }

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        if (!force && !UpdateSettingsStore.ShouldCheck(settings, nowUtc))
        {
            UpdateState cached = await _store.LoadStateAsync(cancellationToken);
            return new UpdateCheckResult(
                cached.Status == UpdateStatus.Unknown ? UpdateStatus.NotDue : cached.Status,
                ProductInfo.Version,
                cached.Manifest,
                "Интервал автоматической проверки ещё не истёк.");
        }

        ReleaseManifest manifest = await FetchManifestAsync(cancellationToken);
        ReleaseVersion current = ProductInfo.ParsedVersion;
        ReleaseVersion latest = manifest.ParsedVersion;
        UpdateStatus status = latest > current
            ? UpdateStatus.Available
            : UpdateStatus.Current;
        string message = status == UpdateStatus.Available
            ? $"Доступна SysDiff {manifest.Version}."
            : $"Установлена актуальная версия SysDiff {ProductInfo.Version}.";

        if (settings.IgnoredVersion is not null
            && settings.IgnoredVersion.Equals(manifest.Version, StringComparison.Ordinal))
        {
            status = UpdateStatus.Current;
            message = $"Версия {manifest.Version} скрыта настройкой ignoredVersion.";
        }

        settings = settings with { LastCheckedAtUtc = nowUtc };
        await _store.SaveSettingsAsync(settings, cancellationToken);
        var state = new UpdateState
        {
            Status = status,
            LastCheckedAtUtc = nowUtc,
            Manifest = manifest
        };
        await _store.SaveStateAsync(state, cancellationToken);

        if (status == UpdateStatus.Available && settings.AutoDownload)
        {
            UpdateDownloadResult downloaded = await DownloadAsync(manifest, cancellationToken);
            return new UpdateCheckResult(
                UpdateStatus.Downloaded,
                ProductInfo.Version,
                downloaded.Manifest,
                $"SysDiff {manifest.Version} автоматически загружена и готова к установке.");
        }

        return new UpdateCheckResult(status, ProductInfo.Version, manifest, message);
    }

    public async Task<UpdateCheckResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        UpdateSettings settings = await _store.LoadSettingsAsync(cancellationToken);
        UpdateState state = await _store.LoadStateAsync(cancellationToken);
        UpdateStatus status = !settings.AutoCheck && state.Status == UpdateStatus.Unknown
            ? UpdateStatus.Disabled
            : state.Status;
        string? message = state.Error;
        if (message is null && state.LastCheckedAtUtc is not null)
        {
            message = $"Последняя проверка: {state.LastCheckedAtUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}.";
        }

        return new UpdateCheckResult(status, ProductInfo.Version, state.Manifest, message);
    }

    public async Task<UpdateDownloadResult> DownloadLatestAsync(
        CancellationToken cancellationToken)
    {
        UpdateCheckResult check = await CheckAsync(force: true, cancellationToken);
        if (check.Manifest is null || check.Manifest.ParsedVersion <= ProductInfo.ParsedVersion)
        {
            throw new InvalidOperationException("Новая stable-версия SysDiff не найдена.");
        }

        return await DownloadAsync(check.Manifest, cancellationToken);
    }

    public async Task<UpdateDownloadResult> DownloadAsync(
        ReleaseManifest manifest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        manifest.Validate();
        EnsureUpdaterCompatibility(manifest);

        string releaseDirectory = Path.Combine(_paths.UpdatesDirectory, manifest.Version);
        string packagePath = Path.Combine(releaseDirectory, manifest.AssetName);
        string temporaryPath = packagePath + ".partial";
        string stagingDirectory = Path.Combine(releaseDirectory, "staging");
        Directory.CreateDirectory(releaseDirectory);

        if (!File.Exists(packagePath)
            || new FileInfo(packagePath).Length != manifest.SizeBytes
            || !VerifyFileHash(packagePath, manifest.Sha256))
        {
            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }

            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            try
            {
                await DownloadFileAsync(manifest, temporaryPath, cancellationToken);
                File.Move(temporaryPath, packagePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        if (!VerifyFileHash(packagePath, manifest.Sha256))
        {
            File.Delete(packagePath);
            throw new InvalidDataException("SHA-256 загруженного release asset не совпадает с manifest.");
        }

        ExtractPackage(packagePath, stagingDirectory);
        string executablePath = Path.Combine(stagingDirectory, "sysdiff.exe");
        if (!File.Exists(executablePath))
        {
            throw new InvalidDataException("В release asset отсутствует sysdiff.exe.");
        }

        await VerifyExecutableVersionAsync(executablePath, manifest.Version, cancellationToken);

        var state = new UpdateState
        {
            Status = UpdateStatus.Downloaded,
            LastCheckedAtUtc = DateTimeOffset.UtcNow,
            Manifest = manifest,
            PackagePath = packagePath,
            StagingDirectory = stagingDirectory
        };
        await _store.SaveStateAsync(state, cancellationToken);
        return new UpdateDownloadResult(manifest, packagePath, stagingDirectory, executablePath);
    }

    public async Task ClearCacheAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(_paths.UpdatesDirectory))
        {
            Directory.Delete(_paths.UpdatesDirectory, recursive: true);
        }

        Directory.CreateDirectory(_paths.UpdatesDirectory);
        await _store.ClearStateAsync(cancellationToken);
    }

    public static bool VerifyFileHash(string path, string expectedSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);
        if (!File.Exists(path))
        {
            return false;
        }

        using FileStream stream = File.OpenRead(path);
        string actual = Convert.ToHexString(SHA256.HashData(stream));
        return actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ReleaseManifest> FetchManifestAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProductInfo.StableManifestUrl);
        request.Headers.UserAgent.ParseAdd($"SysDiff/{ProductInfo.Version}");
        request.Headers.Accept.ParseAdd("application/json");
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > ManifestMaximumBytes)
        {
            throw new InvalidDataException("Release manifest превышает допустимый размер.");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream();
        await CopyWithLimitAsync(stream, memory, ManifestMaximumBytes, cancellationToken);
        string json = System.Text.Encoding.UTF8.GetString(memory.ToArray());
        return ReleaseManifest.Parse(json);
    }

    private async Task DownloadFileAsync(
        ReleaseManifest manifest,
        string destination,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, manifest.AssetUrl);
        request.Headers.UserAgent.ParseAdd($"SysDiff/{ProductInfo.Version}");
        request.Headers.Accept.ParseAdd("application/octet-stream");
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        long? contentLength = response.Content.Headers.ContentLength;
        if (contentLength is > ReleaseManifest.MaximumAssetSizeBytes
            || contentLength is not null && contentLength.Value != manifest.SizeBytes)
        {
            throw new InvalidDataException("Размер HTTP release asset не совпадает с manifest.");
        }

        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream output = new(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        long copied = await CopyWithLimitAsync(
            input,
            output,
            ReleaseManifest.MaximumAssetSizeBytes,
            cancellationToken);
        await output.FlushAsync(cancellationToken);
        if (copied != manifest.SizeBytes)
        {
            throw new InvalidDataException(
                $"Загружено {copied} байт вместо ожидаемых {manifest.SizeBytes}.");
        }
    }

    private static async Task<long> CopyWithLimitAsync(
        Stream input,
        Stream output,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[128 * 1024];
        long total = 0;
        while (true)
        {
            int read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maximumBytes)
            {
                throw new InvalidDataException("Загружаемый файл превышает допустимый размер.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return total;
    }

    private static void ExtractPackage(string packagePath, string stagingDirectory)
    {
        if (Directory.Exists(stagingDirectory))
        {
            Directory.Delete(stagingDirectory, recursive: true);
        }

        Directory.CreateDirectory(stagingDirectory);
        string root = Path.GetFullPath(stagingDirectory) + Path.DirectorySeparatorChar;
        long totalSize = 0;
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            totalSize += entry.Length;
            if (totalSize > ExtractedMaximumBytes)
            {
                throw new InvalidDataException("Распакованный release asset превышает допустимый размер.");
            }

            string destination = Path.GetFullPath(Path.Combine(stagingDirectory, entry.FullName));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Release asset содержит небезопасный путь.");
            }

            if (entry.Name.Length == 0)
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            string? directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    private static async Task VerifyExecutableVersionAsync(
        string executablePath,
        string expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("--version");
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Не удалось запустить staged sysdiff.exe.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            string output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            string error = await process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            if (process.ExitCode != 0
                || !output.Trim().Equals($"SysDiff {expectedVersion}", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Staged executable не прошёл проверку версии. Output: {output.Trim()} {error.Trim()}");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw new TimeoutException("Проверка staged executable превысила 20 секунд.");
        }
    }

    private static void EnsureUpdaterCompatibility(ReleaseManifest manifest)
    {
        ReleaseVersion minimum = ReleaseVersion.Parse(manifest.MinimumUpdaterVersion);
        if (ProductInfo.ParsedVersion < minimum)
        {
            throw new InvalidOperationException(
                $"Для установки {manifest.Version} требуется updater {minimum} или новее.");
        }
    }

    private const long ManifestMaximumBytes = 256 * 1024;
    private const long ExtractedMaximumBytes = 1024L * 1024L * 1024L;
}
