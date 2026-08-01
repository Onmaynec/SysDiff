using System.Text.Json;
using System.Text.Json.Serialization;
using SysDiff.Domain;

namespace SysDiff.Core;

public sealed class ProfileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<CaptureProfile> LoadAsync(
        string path,
        IReadOnlyCollection<string> knownProviders,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Файл профиля не найден.", fullPath);
        }

        await using FileStream stream = new(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        CaptureProfile? profile;
        try
        {
            profile = await JsonSerializer.DeserializeAsync<CaptureProfile>(
                stream,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Некорректный JSON профиля: {exception.Path ?? "$"}: {exception.Message}",
                exception);
        }

        if (profile is null || string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new InvalidDataException("Профиль должен содержать непустое поле name.");
        }

        if (profile.Providers.Count == 0)
        {
            throw new InvalidDataException("Профиль должен включать хотя бы один провайдер.");
        }

        var known = new HashSet<string>(knownProviders, StringComparer.OrdinalIgnoreCase);
        string[] unknown = profile.Providers.Keys.Where(x => !known.Contains(x)).ToArray();
        if (unknown.Length > 0)
        {
            throw new InvalidDataException(
                $"Неизвестные провайдеры: {string.Join(", ", unknown)}.");
        }

        foreach ((string providerId, ProviderOptions options) in profile.Providers)
        {
            if (options.MaximumDepth < 0 || options.MaximumDepth > 256)
            {
                throw new InvalidDataException(
                    $"{providerId}.maximumDepth должен быть в диапазоне 0..256.");
            }

            if (options.MaximumArtifacts <= 0 || options.MaximumArtifacts > 5_000_000)
            {
                throw new InvalidDataException(
                    $"{providerId}.maximumArtifacts должен быть в диапазоне 1..5000000.");
            }
        }

        return profile with { Name = profile.Name.Trim() };
    }
}
