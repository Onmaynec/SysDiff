using SysDiff.Domain;

namespace SysDiff.Core;

public sealed class NoiseFilterEngine : INoiseFilterEngine
{
    private static readonly string[] NoiseFragments =
    [
        @"\Temp\",
        @"\Cache\",
        @"\Logs\",
        @"\INetCache\",
        @"\Prefetch\",
        @"\CrashDumps\",
        @"\Code Cache\",
        @"\GPUCache\"
    ];

    private static readonly string[] NoiseExtensions =
    [
        ".tmp",
        ".log",
        ".etl",
        ".dmp",
        ".cache"
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
}
