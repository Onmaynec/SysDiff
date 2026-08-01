using System.Text.Json;

namespace SysDiff.Cli;

public sealed class UpdateSettingsStore
{
    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public UpdateSettingsStore(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<UpdateSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            UpdateSettings? settings = await ReadAsync<UpdateSettings>(
                _paths.UpdateSettingsPath,
                cancellationToken);
            return Normalize(settings ?? new UpdateSettings());
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveSettingsAsync(
        UpdateSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await WriteAtomicAsync(
                _paths.UpdateSettingsPath,
                Normalize(settings),
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UpdateState> LoadStateAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadAsync<UpdateState>(_paths.UpdateStatePath, cancellationToken)
                ?? new UpdateState();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveStateAsync(UpdateState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await WriteAtomicAsync(_paths.UpdateStatePath, state, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearStateAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_paths.UpdateStatePath))
            {
                File.Delete(_paths.UpdateStatePath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public static bool ShouldCheck(UpdateSettings settings, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.AutoCheck)
        {
            return false;
        }

        return settings.LastCheckedAtUtc is null
            || nowUtc - settings.LastCheckedAtUtc.Value
                >= TimeSpan.FromHours(settings.CheckIntervalHours);
    }

    public static UpdateSettings Normalize(UpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings with
        {
            CheckIntervalHours = Math.Clamp(settings.CheckIntervalHours, 1, 168),
            Channel = "stable",
            IgnoredVersion = string.IsNullOrWhiteSpace(settings.IgnoredVersion)
                ? null
                : ReleaseVersion.Parse(settings.IgnoredVersion).ToString()
        };
    }

    private static async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            string corruptPath = path + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            try
            {
                File.Move(path, corruptPath, overwrite: true);
            }
            catch (IOException)
            {
            }

            return default;
        }
    }

    private static async Task WriteAtomicAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
