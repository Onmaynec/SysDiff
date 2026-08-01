using System.Text.Json;
using SysDiff.Domain;

namespace SysDiff.Providers;

public sealed class FirewallProvider : ISnapshotProvider
{
    private const string Script = """
        $ErrorActionPreference = 'Stop'
        $rules = Get-NetFirewallRule -PolicyStore ActiveStore
        $result = foreach ($rule in $rules) {
            $port = Get-NetFirewallPortFilter -AssociatedNetFirewallRule $rule -ErrorAction SilentlyContinue | Select-Object -First 1
            $address = Get-NetFirewallAddressFilter -AssociatedNetFirewallRule $rule -ErrorAction SilentlyContinue | Select-Object -First 1
            $application = Get-NetFirewallApplicationFilter -AssociatedNetFirewallRule $rule -ErrorAction SilentlyContinue | Select-Object -First 1
            $service = Get-NetFirewallServiceFilter -AssociatedNetFirewallRule $rule -ErrorAction SilentlyContinue | Select-Object -First 1

            [pscustomobject]@{
                Name = [string]$rule.Name
                DisplayName = [string]$rule.DisplayName
                Description = [string]$rule.Description
                Direction = [string]$rule.Direction
                Action = [string]$rule.Action
                Enabled = [string]$rule.Enabled
                Profile = [string]$rule.Profile
                Group = [string]$rule.DisplayGroup
                Protocol = [string]$port.Protocol
                LocalPort = [string]$port.LocalPort
                RemotePort = [string]$port.RemotePort
                LocalAddress = [string]$address.LocalAddress
                RemoteAddress = [string]$address.RemoteAddress
                Program = [string]$application.Program
                Service = [string]$service.Service
            }
        }
        @($result) | ConvertTo-Json -Compress -Depth 4
        """;

    public string Id => "firewall";

    public string DisplayName => "Windows Firewall";

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
                SystemArtifact? artifact = CreateArtifact(item);
                if (artifact is null)
                {
                    continue;
                }

                artifacts.Add(artifact);
                context.Progress?.Report(new SnapshotProgress(
                    Id,
                    "Сканирование правил Firewall",
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

    private static SystemArtifact? CreateArtifact(JsonElement item)
    {
        string? name = PowerShellJsonRunner.ReadString(item, "Name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string displayName = PowerShellJsonRunner.ReadString(item, "DisplayName") ?? name;
        string direction = PowerShellJsonRunner.ReadString(item, "Direction") ?? "Unknown";
        string action = PowerShellJsonRunner.ReadString(item, "Action") ?? "Unknown";
        string enabled = PowerShellJsonRunner.ReadString(item, "Enabled") ?? "Unknown";

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Network"
        };

        if (direction.Equals("Inbound", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("Inbound");
        }

        if (action.Equals("Allow", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("Allow");
        }

        if (enabled.Equals("True", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("Enabled");
        }

        return new SystemArtifact
        {
            ProviderId = "firewall",
            ArtifactType = "FirewallRule",
            Identity = $"firewall://{Uri.EscapeDataString(name)}",
            DisplayName = displayName,
            Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = ArtifactValue.From(name),
                ["DisplayName"] = ArtifactValue.From(displayName),
                ["Description"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "Description")),
                ["Direction"] = ArtifactValue.From(direction),
                ["Action"] = ArtifactValue.From(action),
                ["Enabled"] = ArtifactValue.From(enabled),
                ["Profile"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "Profile")),
                ["Protocol"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "Protocol")),
                ["LocalPort"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "LocalPort")),
                ["RemotePort"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "RemotePort")),
                ["LocalAddress"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "LocalAddress")),
                ["RemoteAddress"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "RemoteAddress")),
                ["Program"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "Program")),
                ["Service"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "Service")),
                ["Group"] = ArtifactValue.From(PowerShellJsonRunner.ReadString(item, "Group"))
            },
            Tags = tags
        };
    }
}
