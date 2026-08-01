using SysDiff.Domain;

namespace SysDiff.Core;

public sealed class NoiseFilterEngine : INoiseFilterEngine
{
    private static readonly string[] NoiseFragments =
    [
        @"\Temp\",
        @"\Cache\",
        @"\Caches\",
        @"\Logs\",
        @"\INetCache\",
        @"\Prefetch\",
        @"\CrashDumps\",
        @"\Code Cache\",
        @"\GPUCache\",
        @"\ShaderCache\",
        @"\Service Worker\CacheStorage\",
        @"\Windows\SoftwareDistribution\Download\",
        @"\Windows\System32\LogFiles\",
        @"\Microsoft\Windows\WebCache\"
    ];

    private static readonly string[] NoiseExtensions =
    [
        ".tmp",
        ".temp",
        ".log",
        ".etl",
        ".dmp",
        ".cache",
        ".old",
        ".bak"
    ];

    public IReadOnlyList<SystemChange> Apply(
        IEnumerable<SystemChange> changes,
        NoiseMode mode,
        out int hiddenCount)
    {
        var visible = new List<SystemChange>();
        hiddenCount = 0;

        foreach (SystemChange change in changes)
        {
            bool isNoise = IsNoise(change);
            bool shouldHide = mode switch
            {
                NoiseMode.Raw => false,
                NoiseMode.Balanced => isNoise && change.Severity <= Severity.Low,
                NoiseMode.Strict => change.Severity < Severity.Medium,
                _ => false
            };

            if (shouldHide)
            {
                hiddenCount++;
                continue;
            }

            visible.Add(change with { IsNoise = isNoise });
        }

        return visible
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.ProviderId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsNoise(SystemChange change)
    {
        if (change.ProviderId.Equals("services", StringComparison.OrdinalIgnoreCase))
        {
            return change.ChangeType == ChangeType.Modified
                && HasOnlyProperties(change, "Status");
        }

        if (change.ProviderId.Equals("drivers", StringComparison.OrdinalIgnoreCase))
        {
            return change.ChangeType == ChangeType.Modified
                && HasOnlyProperties(change, "State", "Status", "Started");
        }

        if (!change.ProviderId.Equals("filesystem", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string candidate = change.After?.Properties.GetValueOrDefault("Path")?.Value
            ?? change.Before?.Properties.GetValueOrDefault("Path")?.Value
            ?? change.Identity;

        if (NoiseFragments.Any(fragment =>
            candidate.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string extension = Path.GetExtension(candidate);
        return NoiseExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasOnlyProperties(SystemChange change, params string[] names)
    {
        if (change.ChangedProperties.Count == 0)
        {
            return false;
        }

        var allowed = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        return change.ChangedProperties.All(x => allowed.Contains(x.Name));
    }
}
