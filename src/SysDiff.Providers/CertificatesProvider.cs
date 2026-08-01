using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SysDiff.Domain;

namespace SysDiff.Providers;

public sealed class CertificatesProvider : ISnapshotProvider
{
    private static readonly string[] StoreNames =
    [
        "My",
        "Root",
        "CA",
        "AuthRoot",
        "TrustedPublisher",
        "TrustedPeople"
    ];

    public string Id => "certificates";

    public string DisplayName => "Сертификаты Windows";

    public bool RequiresAdministrator => false;

    public Task<ProviderSnapshotResult> CaptureAsync(
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var artifacts = new List<SystemArtifact>();
        var warnings = new List<string>();

        foreach (StoreLocation location in Enum.GetValues<StoreLocation>())
        {
            foreach (string storeName in StoreNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CaptureStore(location, storeName, artifacts, warnings, context, cancellationToken);
            }
        }

        return Task.FromResult(new ProviderSnapshotResult
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            Status = warnings.Count > 0 ? ProviderStatus.Partial : ProviderStatus.Success,
            StartedAtUtc = started,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            ArtifactCount = artifacts.Count,
            Artifacts = artifacts,
            Warnings = warnings,
            RequiresAdministrator = RequiresAdministrator
        });
    }

    private static void CaptureStore(
        StoreLocation location,
        string storeName,
        List<SystemArtifact> artifacts,
        List<string> warnings,
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            using var store = new X509Store(storeName, location);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            foreach (X509Certificate2 certificate in store.Certificates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (certificate)
                {
                    artifacts.Add(CreateArtifact(location, storeName, certificate));
                }

                context.Progress?.Report(new SnapshotProgress(
                    "certificates",
                    "Сканирование сертификатов",
                    artifacts.Count,
                    $"{location}/{storeName}"));
            }
        }
        catch (Exception exception) when (
            exception is CryptographicException
            or UnauthorizedAccessException
            or PlatformNotSupportedException)
        {
            warnings.Add($"{location}/{storeName}: {exception.Message}");
        }
    }

    private static SystemArtifact CreateArtifact(
        StoreLocation location,
        string storeName,
        X509Certificate2 certificate)
    {
        string thumbprint = certificate.Thumbprint ?? certificate.GetCertHashString(HashAlgorithmName.SHA256);
        string subjectName = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        string displayName = string.IsNullOrWhiteSpace(subjectName)
            ? certificate.Subject
            : subjectName;
        string usages = string.Join(
            ';',
            certificate.Extensions
                .OfType<X509EnhancedKeyUsageExtension>()
                .SelectMany(x => x.EnhancedKeyUsages.Cast<Oid>())
                .Select(x => x.FriendlyName ?? x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        bool trusted = IsTrusted(certificate);
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Certificate",
            location.ToString(),
            storeName
        };

        if (certificate.HasPrivateKey)
        {
            tags.Add("HasPrivateKey");
        }

        if (trusted)
        {
            tags.Add("Trusted");
        }

        return new SystemArtifact
        {
            ProviderId = "certificates",
            ArtifactType = "Certificate",
            Identity = $"certificate://{location}/{Uri.EscapeDataString(storeName)}/{thumbprint}",
            DisplayName = displayName,
            Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["StoreLocation"] = ArtifactValue.From(location),
                ["StoreName"] = ArtifactValue.From(storeName),
                ["Subject"] = ArtifactValue.From(certificate.Subject),
                ["Issuer"] = ArtifactValue.From(certificate.Issuer),
                ["Thumbprint"] = ArtifactValue.From(thumbprint),
                ["SerialNumber"] = ArtifactValue.From(certificate.SerialNumber),
                ["NotBefore"] = ArtifactValue.From(certificate.NotBefore.ToUniversalTime(), "DateTime"),
                ["NotAfter"] = ArtifactValue.From(certificate.NotAfter.ToUniversalTime(), "DateTime"),
                ["EnhancedKeyUsages"] = ArtifactValue.From(usages),
                ["HasPrivateKey"] = ArtifactValue.From(certificate.HasPrivateKey, "Boolean"),
                ["Trusted"] = ArtifactValue.From(trusted, "Boolean"),
                ["SignatureAlgorithm"] = ArtifactValue.From(certificate.SignatureAlgorithm?.FriendlyName),
                ["PublicKeyAlgorithm"] = ArtifactValue.From(certificate.PublicKey?.Oid?.FriendlyName)
            },
            Tags = tags
        };
    }

    private static bool IsTrusted(X509Certificate2 certificate)
    {
        try
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            chain.ChainPolicy.DisableCertificateDownloads = true;
            return chain.Build(certificate);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
