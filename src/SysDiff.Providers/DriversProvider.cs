using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using SysDiff.Domain;

namespace SysDiff.Providers;

public sealed class DriversProvider : ISnapshotProvider
{
    private const string Script = """
        $ErrorActionPreference = 'Stop'
        $items = Get-CimInstance Win32_SystemDriver | ForEach-Object {
            [pscustomobject]@{
                Name = [string]$_.Name
                DisplayName = [string]$_.DisplayName
                Description = [string]$_.Description
                State = [string]$_.State
                Status = [string]$_.Status
                StartMode = [string]$_.StartMode
                ServiceType = [string]$_.ServiceType
                PathName = [string]$_.PathName
                Started = [string]$_.Started
            }
        }
        @($items) | ConvertTo-Json -Compress -Depth 3
        """;

    public string Id => "drivers";

    public string DisplayName => "Системные драйверы";

    public bool RequiresAdministrator => false;

    public async Task<ProviderSnapshotResult> CaptureAsync(
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var artifacts = new List<SystemArtifact>();
        var warnings = new List<string>();

        try
        {
            using JsonDocument document = await PowerShellJsonRunner.RunAsync(
                Script,
                TimeSpan.FromMinutes(2),
                cancellationToken);

            foreach (JsonElement item in PowerShellJsonRunner.EnumerateObjects(document.RootElement))
            {
                cancellationToken.ThrowIfCancellationRequested();
                SystemArtifact? artifact = await CreateArtifactAsync(item, warnings, cancellationToken);
                if (artifact is null)
                {
                    continue;
                }

                artifacts.Add(artifact);
                context.Progress?.Report(new SnapshotProgress(
                    Id,
                    "Сканирование драйверов",
                    artifacts.Count,
                    artifact.DisplayName));
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or TimeoutException
            or JsonException
            or PlatformNotSupportedException)
        {
            warnings.Add(exception.Message);
        }

        return new ProviderSnapshotResult
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
        };
    }

    private static async Task<SystemArtifact?> CreateArtifactAsync(
        JsonElement item,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        string? name = PowerShellJsonRunner.ReadString(item, "Name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string displayName = PowerShellJsonRunner.ReadString(item, "DisplayName") ?? name;
        string? rawPath = PowerShellJsonRunner.ReadString(item, "PathName");
        string? binaryPath = ResolveBinaryPath(rawPath);
        string? sha256 = null;
        string? fileVersion = null;
        string? publisher = null;
        string signature = "Unavailable";
        string? signerThumbprint = null;

        if (!string.IsNullOrWhiteSpace(binaryPath) && File.Exists(binaryPath))
        {
            try
            {
                await using FileStream stream = new(
                    binaryPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 128 * 1024,
                    useAsync: true);
                byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
                sha256 = Convert.ToHexString(hash);

                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(binaryPath);
                fileVersion = versionInfo.FileVersion;
                publisher = versionInfo.CompanyName;

                try
                {
                    using X509Certificate certificate = X509Certificate.CreateFromSignedFile(binaryPath);
                    using var certificate2 = new X509Certificate2(certificate);
                    signature = "Present";
                    signerThumbprint = certificate2.Thumbprint;
                    publisher ??= certificate2.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
                }
                catch (CryptographicException)
                {
                    signature = "MissingOrInvalid";
                }
            }
            catch (Exception exception) when (
                exception is IOException
                or UnauthorizedAccessException
                or CryptographicException)
            {
                warnings.Add($"{name}: {exception.Message}");
            }
        }

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Kernel",
            "Persistence"
        };

        if (signature == "MissingOrInvalid")
        {
            tags.Add("Unsigned");
        }

        return new SystemArtifact
        {
            ProviderId = "drivers",
            ArtifactType = "SystemDriver",
            Identity = $"driver://{Uri.EscapeDataString(name)}",
            DisplayName = displayName,
            Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = ArtifactValue.From(name),
                ["DisplayName"] = ArtifactValue.From(displayName),
                ["Description"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "Description")),
                ["State"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "State")),
                ["Status"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "Status")),
                ["StartMode"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "StartMode")),
                ["ServiceType"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "ServiceType")),
                ["Started"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "Started")),
                ["RawPath"] = ArtifactValue.From(rawPath),
                ["BinaryPath"] = ArtifactValue.From(binaryPath),
                ["FileVersion"] = ArtifactValue.From(fileVersion),
                ["Publisher"] = ArtifactValue.From(publisher),
                ["Signature"] = ArtifactValue.From(signature),
                ["SignerThumbprint"] = ArtifactValue.From(signerThumbprint),
                ["Sha256"] = ArtifactValue.From(sha256)
            },
            Tags = tags
        };
    }

    private static string? ResolveBinaryPath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        string value = Environment.ExpandEnvironmentVariables(rawPath.Trim());
        if (value.StartsWith("\\??\\", StringComparison.Ordinal))
        {
            value = value[4..];
        }

        if (value.StartsWith('"'))
        {
            int closingQuote = value.IndexOf('"', 1);
            value = closingQuote > 1 ? value[1..closingQuote] : value.Trim('"');
        }
        else
        {
            int sysIndex = value.IndexOf(".sys", StringComparison.OrdinalIgnoreCase);
            if (sysIndex >= 0)
            {
                value = value[..(sysIndex + 4)];
            }
        }

        if (value.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
        {
            value = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                value[12..]);
        }
        else if (value.StartsWith(@"System32\", StringComparison.OrdinalIgnoreCase))
        {
            value = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                value);
        }

        try
        {
            return Path.GetFullPath(value);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return value;
        }
    }
}
