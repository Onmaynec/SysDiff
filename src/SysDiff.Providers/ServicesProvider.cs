using System.ServiceProcess;
using Microsoft.Win32;
using SysDiff.Domain;

namespace SysDiff.Providers;

public sealed class ServicesProvider : ISnapshotProvider
{
    public string Id => "services";

    public string DisplayName => "Службы Windows";

    public bool RequiresAdministrator => false;

    public Task<ProviderSnapshotResult> CaptureAsync(
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var artifacts = new List<SystemArtifact>();
        var warnings = new List<string>();

        try
        {
            foreach (ServiceController service in ServiceController.GetServices())
            {
                cancellationToken.ThrowIfCancellationRequested();
                string serviceName = service.ServiceName;

                using (service)
                {
                    artifacts.Add(CreateArtifact(service, warnings));
                }

                context.Progress?.Report(new SnapshotProgress(
                    Id,
                    "Сканирование служб",
                    artifacts.Count,
                    serviceName));
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or UnauthorizedAccessException)
        {
            warnings.Add(exception.Message);
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

    private static SystemArtifact CreateArtifact(
        ServiceController service,
        List<string> warnings)
    {
        var properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = ArtifactValue.From(service.ServiceName),
            ["DisplayName"] = ArtifactValue.From(service.DisplayName),
            ["Status"] = ArtifactValue.From(SafeRead(() => service.Status.ToString())),
            ["ServiceType"] = ArtifactValue.From(SafeRead(() => service.ServiceType.ToString())),
            ["CanStop"] = ArtifactValue.From(SafeRead(() => service.CanStop), "Boolean")
        };

        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Services\{service.ServiceName}",
                writable: false);

            if (key is not null)
            {
                properties["BinaryPath"] = ArtifactValue.From(
                    key.GetValue("ImagePath", null, RegistryValueOptions.DoNotExpandEnvironmentNames));
                properties["StartType"] = ArtifactValue.From(MapStartType(key.GetValue("Start")));
                properties["Account"] = ArtifactValue.From(key.GetValue("ObjectName"));
                properties["Description"] = ArtifactValue.From(key.GetValue("Description"));
                properties["Dependencies"] = ArtifactValue.From(
                    JoinRegistryValue(key.GetValue("DependOnService")));
            }
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException)
        {
            warnings.Add($"{service.ServiceName}: {exception.Message}");
        }

        return new SystemArtifact
        {
            ProviderId = "services",
            ArtifactType = "WindowsService",
            Identity = $"service://{Uri.EscapeDataString(service.ServiceName)}",
            DisplayName = service.DisplayName,
            Properties = properties,
            Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Persistence"
            }
        };
    }

    private static object? SafeRead(Func<object?> reader)
    {
        try
        {
            return reader();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static string MapStartType(object? raw)
    {
        if (raw is null)
        {
            return "Unknown";
        }

        int value = Convert.ToInt32(raw, System.Globalization.CultureInfo.InvariantCulture);
        return value switch
        {
            0 => "Boot",
            1 => "System",
            2 => "Automatic",
            3 => "Manual",
            4 => "Disabled",
            _ => $"Unknown({value})"
        };
    }

    private static string? JoinRegistryValue(object? value) => value switch
    {
        string text => text,
        string[] values => string.Join(';', values),
        _ => value?.ToString()
    };
}
