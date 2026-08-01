using Microsoft.Win32;
using SysDiff.Domain;

namespace SysDiff.Providers;

public sealed class InstalledAppsProvider : ISnapshotProvider
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public string Id => "installed-apps";

    public string DisplayName => "Установленные приложения";

    public bool RequiresAdministrator => false;

    public Task<ProviderSnapshotResult> CaptureAsync(
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var artifacts = new List<SystemArtifact>();
        var warnings = new List<string>();

        CaptureHive(RegistryHive.LocalMachine, RegistryView.Registry64, "machine", artifacts, warnings, context, cancellationToken);
        CaptureHive(RegistryHive.LocalMachine, RegistryView.Registry32, "machine", artifacts, warnings, context, cancellationToken);
        CaptureHive(RegistryHive.CurrentUser, RegistryView.Registry64, "user", artifacts, warnings, context, cancellationToken);
        CaptureHive(RegistryHive.CurrentUser, RegistryView.Registry32, "user", artifacts, warnings, context, cancellationToken);

        List<SystemArtifact> distinct = artifacts
            .GroupBy(x => x.Identity, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(new ProviderSnapshotResult
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            Status = warnings.Count > 0 ? ProviderStatus.Partial : ProviderStatus.Success,
            StartedAtUtc = started,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            ArtifactCount = distinct.Count,
            Artifacts = distinct,
            Warnings = warnings,
            RequiresAdministrator = RequiresAdministrator
        });
    }

    private static void CaptureHive(
        RegistryHive hive,
        RegistryView view,
        string scope,
        List<SystemArtifact> artifacts,
        List<string> warnings,
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? uninstall = baseKey.OpenSubKey(UninstallPath, writable: false);
            if (uninstall is null)
            {
                return;
            }

            foreach (string subKeyName in uninstall.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using RegistryKey? application = uninstall.OpenSubKey(subKeyName, writable: false);
                    if (application is null)
                    {
                        continue;
                    }

                    string? displayName = application.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        continue;
                    }

                    string architecture = view == RegistryView.Registry64 ? "x64" : "x86";
                    string identityKey = application.GetValue("ProductID")?.ToString()
                        ?? application.GetValue("BundleProviderKey")?.ToString()
                        ?? subKeyName;

                    var properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["DisplayName"] = ArtifactValue.From(displayName),
                        ["DisplayVersion"] = ArtifactValue.From(application.GetValue("DisplayVersion")),
                        ["Publisher"] = ArtifactValue.From(application.GetValue("Publisher")),
                        ["InstallDate"] = ArtifactValue.From(application.GetValue("InstallDate")),
                        ["InstallLocation"] = ArtifactValue.From(application.GetValue("InstallLocation")),
                        ["UninstallString"] = ArtifactValue.From(application.GetValue("UninstallString")),
                        ["QuietUninstallString"] = ArtifactValue.From(application.GetValue("QuietUninstallString")),
                        ["ProductId"] = ArtifactValue.From(identityKey),
                        ["Scope"] = ArtifactValue.From(scope),
                        ["Architecture"] = ArtifactValue.From(architecture),
                        ["Source"] = ArtifactValue.From("Registry"),
                        ["SystemComponent"] = ArtifactValue.From(application.GetValue("SystemComponent"))
                    };

                    artifacts.Add(new SystemArtifact
                    {
                        ProviderId = "installed-apps",
                        ArtifactType = "InstalledApplication",
                        Identity = $"app://{scope}/{architecture}/{Uri.EscapeDataString(identityKey)}",
                        DisplayName = displayName,
                        Properties = properties,
                        Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "Application",
                            scope
                        }
                    });

                    context.Progress?.Report(new SnapshotProgress(
                        "installed-apps",
                        "Сканирование установленных приложений",
                        artifacts.Count,
                        displayName));
                }
                catch (Exception exception) when (
                    exception is UnauthorizedAccessException
                    or System.Security.SecurityException
                    or IOException)
                {
                    warnings.Add($"{scope}/{view}/{subKeyName}: {exception.Message}");
                }
            }
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException
            or PlatformNotSupportedException)
        {
            warnings.Add($"{scope}/{view}: {exception.Message}");
        }
    }
}
