using Microsoft.Win32;
using SysDiff.Domain;

namespace SysDiff.Providers;

public sealed class StartupProvider : ISnapshotProvider
{
    private static readonly (RegistryHive Hive, RegistryView View, string Path, string Scope)[] RegistryLocations =
    [
        (RegistryHive.CurrentUser, RegistryView.Default,
            @"Software\Microsoft\Windows\CurrentVersion\Run", "user"),
        (RegistryHive.CurrentUser, RegistryView.Default,
            @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "user"),
        (RegistryHive.LocalMachine, RegistryView.Registry64,
            @"Software\Microsoft\Windows\CurrentVersion\Run", "machine64"),
        (RegistryHive.LocalMachine, RegistryView.Registry32,
            @"Software\Microsoft\Windows\CurrentVersion\Run", "machine32"),
        (RegistryHive.LocalMachine, RegistryView.Registry64,
            @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "machine64"),
        (RegistryHive.LocalMachine, RegistryView.Registry32,
            @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "machine32")
    ];

    public string Id => "startup";

    public string DisplayName => "Автозагрузка";

    public bool RequiresAdministrator => false;

    public Task<ProviderSnapshotResult> CaptureAsync(
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var artifacts = new List<SystemArtifact>();
        var warnings = new List<string>();

        foreach ((RegistryHive hive, RegistryView view, string path, string scope) in RegistryLocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? key = baseKey.OpenSubKey(path, writable: false);
                if (key is null)
                {
                    continue;
                }

                foreach (string valueName in key.GetValueNames())
                {
                    object? command = key.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);

                    artifacts.Add(new SystemArtifact
                    {
                        ProviderId = Id,
                        ArtifactType = "RegistryStartup",
                        Identity = $"startup://registry/{scope}/{path.Replace('\\', '/')}/{Uri.EscapeDataString(valueName)}",
                        DisplayName = string.IsNullOrWhiteSpace(valueName) ? "(По умолчанию)" : valueName,
                        Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Scope"] = ArtifactValue.From(scope),
                            ["RegistryPath"] = ArtifactValue.From($"{hive}\\{path}"),
                            ["Name"] = ArtifactValue.From(valueName),
                            ["Command"] = ArtifactValue.From(command)
                        },
                        Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "Persistence",
                            "LogonTrigger"
                        }
                    });
                }
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                or System.Security.SecurityException
                or IOException)
            {
                warnings.Add($"{hive}\\{path}: {exception.Message}");
            }
        }

        AddStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            "user",
            artifacts,
            warnings,
            cancellationToken);

        AddStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            "machine",
            artifacts,
            warnings,
            cancellationToken);

        AddWinlogonValue("Shell", artifacts, warnings);
        AddWinlogonValue("Userinit", artifacts, warnings);

        return Task.FromResult(new ProviderSnapshotResult
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            Status = warnings.Count > 0 ? ProviderStatus.Partial : ProviderStatus.Success,
            StartedAtUtc = started,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            ArtifactCount = artifacts.Count,
            Artifacts = artifacts,
            Warnings = warnings
        });
    }

    private static void AddStartupFolder(
        string folder,
        string scope,
        List<SystemArtifact> artifacts,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();

                artifacts.Add(new SystemArtifact
                {
                    ProviderId = "startup",
                    ArtifactType = "StartupFolderItem",
                    Identity = $"startup://folder/{scope}/{Uri.EscapeDataString(Path.GetFileName(file))}",
                    DisplayName = Path.GetFileName(file),
                    Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Scope"] = ArtifactValue.From(scope),
                        ["Path"] = ArtifactValue.From(file),
                        ["Extension"] = ArtifactValue.From(Path.GetExtension(file))
                    },
                    Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "Persistence",
                        "LogonTrigger"
                    }
                });
            }
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
            or IOException)
        {
            warnings.Add($"{folder}: {exception.Message}");
        }
    }

    private static void AddWinlogonValue(
        string valueName,
        List<SystemArtifact> artifacts,
        List<string> warnings)
    {
        const string path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";

        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
            object? value = key?.GetValue(
                valueName,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);

            artifacts.Add(new SystemArtifact
            {
                ProviderId = "startup",
                ArtifactType = "Winlogon",
                Identity = $"startup://winlogon/{valueName.ToLowerInvariant()}",
                DisplayName = $"Winlogon {valueName}",
                Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
                {
                    ["RegistryPath"] = ArtifactValue.From($@"HKLM\{path}"),
                    ["Name"] = ArtifactValue.From(valueName),
                    ["Command"] = ArtifactValue.From(value)
                },
                Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Persistence",
                    "Sensitive"
                }
            });
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException)
        {
            warnings.Add($@"HKLM\{path}\{valueName}: {exception.Message}");
        }
    }
}
