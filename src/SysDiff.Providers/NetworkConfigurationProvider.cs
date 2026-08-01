using System.Net.NetworkInformation;
using System.Text.Json;
using Microsoft.Win32;
using SysDiff.Domain;

namespace SysDiff.Providers;

public sealed class NetworkConfigurationProvider : ISnapshotProvider
{
    public string Id => "network-configuration";

    public string DisplayName => "Сетевая конфигурация";

    public bool RequiresAdministrator => false;

    public async Task<ProviderSnapshotResult> CaptureAsync(
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var artifacts = new List<SystemArtifact>();
        var warnings = new List<string>();

        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                IPInterfaceProperties properties = adapter.GetIPProperties();
                string id = string.IsNullOrWhiteSpace(adapter.Id) ? adapter.Name : adapter.Id;
                artifacts.Add(new SystemArtifact
                {
                    ProviderId = Id,
                    ArtifactType = "NetworkAdapter",
                    Identity = $"network-adapter://{Uri.EscapeDataString(id)}",
                    DisplayName = adapter.Name,
                    Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Name"] = ArtifactValue.From(adapter.Name),
                        ["Description"] = ArtifactValue.From(adapter.Description),
                        ["InterfaceType"] = ArtifactValue.From(adapter.NetworkInterfaceType),
                        ["OperationalStatus"] = ArtifactValue.From(adapter.OperationalStatus),
                        ["Speed"] = ArtifactValue.From(adapter.Speed, "Int64"),
                        ["MacAddress"] = ArtifactValue.From(adapter.GetPhysicalAddress().ToString()),
                        ["DnsSuffix"] = ArtifactValue.From(properties.DnsSuffix),
                        ["DnsServers"] = ArtifactValue.From(
                            string.Join(';', properties.DnsAddresses.Select(x => x.ToString()))),
                        ["Gateways"] = ArtifactValue.From(
                            string.Join(';', properties.GatewayAddresses.Select(x => x.Address.ToString()))),
                        ["UnicastAddresses"] = ArtifactValue.From(
                            string.Join(';', properties.UnicastAddresses.Select(x => x.Address.ToString())))
                    }
                });
            }
            catch (Exception exception) when (
                exception is NetworkInformationException
                or InvalidOperationException)
            {
                warnings.Add($"{adapter.Name}: {exception.Message}");
            }
        }

        CaptureProxy(artifacts, warnings);
        await CaptureRoutesAsync(artifacts, warnings, cancellationToken);

        return new ProviderSnapshotResult
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            Status = warnings.Count == 0 ? ProviderStatus.Success : ProviderStatus.Partial,
            StartedAtUtc = started,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            ArtifactCount = artifacts.Count,
            Artifacts = artifacts,
            Warnings = warnings,
            RequiresAdministrator = RequiresAdministrator
        };
    }

    private static void CaptureProxy(
        ICollection<SystemArtifact> artifacts,
        ICollection<string> warnings)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings",
                writable: false);
            if (key is null)
            {
                return;
            }

            artifacts.Add(new SystemArtifact
            {
                ProviderId = "network-configuration",
                ArtifactType = "SystemProxy",
                Identity = "network-proxy://current-user",
                DisplayName = "Прокси текущего пользователя",
                Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Enabled"] = ArtifactValue.From(Convert.ToInt32(key.GetValue("ProxyEnable", 0)) != 0, "Boolean"),
                    ["Server"] = ArtifactValue.From(key.GetValue("ProxyServer")),
                    ["Override"] = ArtifactValue.From(key.GetValue("ProxyOverride")),
                    ["AutoConfigUrl"] = ArtifactValue.From(key.GetValue("AutoConfigURL"))
                }
            });
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException)
        {
            warnings.Add($"Proxy: {exception.Message}");
        }
    }

    private static async Task CaptureRoutesAsync(
        ICollection<SystemArtifact> artifacts,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        const string script = """
            $ErrorActionPreference = 'Stop'
            Get-NetRoute -AddressFamily IPv4,IPv6 |
              Select-Object InterfaceIndex,DestinationPrefix,NextHop,RouteMetric,Protocol,State |
              ConvertTo-Json -Depth 4 -Compress
            """;

        try
        {
            using JsonDocument document = await PowerShellJsonRunner.RunAsync(
                script,
                TimeSpan.FromSeconds(30),
                cancellationToken);
            foreach (JsonElement item in PowerShellJsonRunner.EnumerateObjects(document.RootElement))
            {
                string interfaceIndex = PowerShellJsonRunner.ReadString(item, "InterfaceIndex") ?? "unknown";
                string destination = PowerShellJsonRunner.ReadString(item, "DestinationPrefix") ?? "unknown";
                string nextHop = PowerShellJsonRunner.ReadString(item, "NextHop") ?? "unknown";
                artifacts.Add(new SystemArtifact
                {
                    ProviderId = "network-configuration",
                    ArtifactType = "NetworkRoute",
                    Identity = $"network-route://{Uri.EscapeDataString(interfaceIndex)}/{Uri.EscapeDataString(destination)}/{Uri.EscapeDataString(nextHop)}",
                    DisplayName = $"{destination} → {nextHop}",
                    Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["InterfaceIndex"] = ArtifactValue.From(interfaceIndex),
                        ["DestinationPrefix"] = ArtifactValue.From(destination),
                        ["NextHop"] = ArtifactValue.From(nextHop),
                        ["RouteMetric"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "RouteMetric")),
                        ["Protocol"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "Protocol")),
                        ["State"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "State"))
                    }
                });
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or TimeoutException
            or JsonException
            or PlatformNotSupportedException)
        {
            warnings.Add($"Routes: {exception.Message}");
        }
    }
}
