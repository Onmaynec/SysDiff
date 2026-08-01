using System.Net.NetworkInformation;
using SysDiff.Domain;

namespace SysDiff.Cli;

internal sealed class NetworkLiveMonitor
{
    public async Task<IReadOnlyList<LiveEvent>> MonitorAsync(
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + duration;
        Dictionary<string, EndpointInfo> previous = Capture();
        var events = new List<LiveEvent>();

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
            Dictionary<string, EndpointInfo> current = Capture();

            foreach ((string identity, EndpointInfo endpoint) in current)
            {
                if (!previous.ContainsKey(identity))
                {
                    events.Add(ToEvent("Opened", endpoint));
                }
            }

            foreach ((string identity, EndpointInfo endpoint) in previous)
            {
                if (!current.ContainsKey(identity))
                {
                    events.Add(ToEvent("Closed", endpoint));
                }
            }

            previous = current;
            if (events.Count >= 100_000)
            {
                break;
            }
        }

        return events;
    }

    private static Dictionary<string, EndpointInfo> Capture()
    {
        IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
        var result = new Dictionary<string, EndpointInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (TcpConnectionInformation connection in properties.GetActiveTcpConnections())
        {
            var endpoint = new EndpointInfo(
                Protocol: "TCP",
                Local: connection.LocalEndPoint.ToString(),
                Remote: connection.RemoteEndPoint.ToString(),
                State: connection.State.ToString());
            result[endpoint.Identity] = endpoint;
        }

        foreach (System.Net.IPEndPoint listener in properties.GetActiveUdpListeners())
        {
            var endpoint = new EndpointInfo(
                Protocol: "UDP",
                Local: listener.ToString(),
                Remote: null,
                State: "Listening");
            result[endpoint.Identity] = endpoint;
        }

        return result;
    }

    private static LiveEvent ToEvent(string eventType, EndpointInfo endpoint) => new()
    {
        TimestampUtc = DateTimeOffset.UtcNow,
        Category = "network",
        EventType = eventType,
        Identity = endpoint.Identity,
        DisplayName = $"{endpoint.Protocol} {endpoint.Local}",
        Properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Protocol"] = endpoint.Protocol,
            ["LocalEndpoint"] = endpoint.Local,
            ["RemoteEndpoint"] = endpoint.Remote,
            ["State"] = endpoint.State
        }
    };

    private sealed record EndpointInfo(
        string Protocol,
        string Local,
        string? Remote,
        string State)
    {
        public string Identity =>
            $"network://{Protocol.ToLowerInvariant()}/{Local}/{Remote ?? "*"}/{State}";
    }
}
