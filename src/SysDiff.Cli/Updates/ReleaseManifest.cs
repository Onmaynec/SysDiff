using System.Text.Json;

namespace SysDiff.Cli;

public sealed record ReleaseManifest
{
    public int SchemaVersion { get; init; } = 1;

    public required string Product { get; init; }

    public required string Version { get; init; }

    public required string Channel { get; init; }

    public required string Runtime { get; init; }

    public required string Tag { get; init; }

    public required string AssetName { get; init; }

    public required string AssetUrl { get; init; }

    public required string Sha256 { get; init; }

    public long SizeBytes { get; init; }

    public required string MinimumUpdaterVersion { get; init; }

    public DateTimeOffset PublishedAtUtc { get; init; }

    public bool Unsigned { get; init; } = true;

    public ReleaseVersion ParsedVersion => ReleaseVersion.Parse(Version);

    public static ReleaseManifest Parse(string json, string expectedRuntime = ProductInfo.Runtime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ReleaseManifest manifest = JsonSerializer.Deserialize<ReleaseManifest>(
            json,
            JsonOptions) ?? throw new ReleaseManifestException("Manifest пуст или не является JSON-объектом.");
        manifest.Validate(expectedRuntime);
        return manifest;
    }

    public void Validate(string expectedRuntime = ProductInfo.Runtime)
    {
        if (SchemaVersion != 1)
        {
            throw new ReleaseManifestException($"Неподдерживаемая schemaVersion: {SchemaVersion}.");
        }

        if (!Product.Equals(ProductInfo.Name, StringComparison.Ordinal))
        {
            throw new ReleaseManifestException("Manifest относится к другому продукту.");
        }

        ReleaseVersion version;
        ReleaseVersion minimumUpdater;
        try
        {
            version = ReleaseVersion.Parse(Version);
            minimumUpdater = ReleaseVersion.Parse(MinimumUpdaterVersion);
        }
        catch (FormatException exception)
        {
            throw new ReleaseManifestException(exception.Message, exception);
        }

        if (version.IsPreRelease || !Channel.Equals(ProductInfo.Channel, StringComparison.OrdinalIgnoreCase))
        {
            throw new ReleaseManifestException("Stable updater принимает только stable-релизы.");
        }

        if (!Runtime.Equals(expectedRuntime, StringComparison.OrdinalIgnoreCase))
        {
            throw new ReleaseManifestException(
                $"Runtime {Runtime} не поддерживается текущей сборкой {expectedRuntime}.");
        }

        string normalizedVersion = version.ToString();
        string expectedTag = $"v{normalizedVersion}";
        string expectedAsset = $"SysDiff-{normalizedVersion}-{expectedRuntime}.zip";
        if (!Tag.Equals(expectedTag, StringComparison.Ordinal))
        {
            throw new ReleaseManifestException($"Tag {Tag} не совпадает с версией {normalizedVersion}.");
        }

        if (!AssetName.Equals(expectedAsset, StringComparison.Ordinal))
        {
            throw new ReleaseManifestException($"Неожиданное имя release asset: {AssetName}.");
        }

        if (SizeBytes is <= 0 or > MaximumAssetSizeBytes)
        {
            throw new ReleaseManifestException("Размер release asset находится вне допустимого диапазона.");
        }

        if (Sha256.Length != 64 || Sha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ReleaseManifestException("SHA-256 должен содержать 64 шестнадцатеричных символа.");
        }

        if (!Uri.TryCreate(AssetUrl, UriKind.Absolute, out Uri? assetUri)
            || assetUri.Scheme != Uri.UriSchemeHttps
            || !assetUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new ReleaseManifestException("Release asset должен использовать официальный HTTPS URL GitHub.");
        }

        string expectedPath = $"/{ProductInfo.Repository}/releases/download/{expectedTag}/{expectedAsset}";
        if (!assetUri.AbsolutePath.Equals(expectedPath, StringComparison.Ordinal))
        {
            throw new ReleaseManifestException("Release asset URL не соответствует официальному репозиторию и tag.");
        }

        if (minimumUpdater > version)
        {
            throw new ReleaseManifestException("minimumUpdaterVersion не может быть новее самого релиза.");
        }

        if (PublishedAtUtc == default)
        {
            throw new ReleaseManifestException("publishedAtUtc обязателен.");
        }
    }

    public const long MaximumAssetSizeBytes = 512L * 1024L * 1024L;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

public sealed class ReleaseManifestException : IOException
{
    public ReleaseManifestException(string message)
        : base(message)
    {
    }

    public ReleaseManifestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
