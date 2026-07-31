using SysDiff.Domain;

namespace SysDiff.Core;

public sealed class ComparisonEngine
{
    private readonly ISeverityEngine _severityEngine;
    private readonly INoiseFilterEngine _noiseFilterEngine;

    public ComparisonEngine(
        ISeverityEngine severityEngine,
        INoiseFilterEngine noiseFilterEngine)
    {
        _severityEngine = severityEngine;
        _noiseFilterEngine = noiseFilterEngine;
    }

    public ComparisonResult Compare(
        SnapshotRecord before,
        SnapshotRecord after,
        NoiseMode noiseMode)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var beforeMap = before.Artifacts.ToDictionary(
            x => x.Identity,
            StringComparer.OrdinalIgnoreCase);
        var afterMap = after.Artifacts.ToDictionary(
            x => x.Identity,
            StringComparer.OrdinalIgnoreCase);

        var changes = new List<SystemChange>();

        foreach (string identity in beforeMap.Keys.Union(afterMap.Keys, StringComparer.OrdinalIgnoreCase))
        {
            beforeMap.TryGetValue(identity, out SystemArtifact? oldArtifact);
            afterMap.TryGetValue(identity, out SystemArtifact? newArtifact);

            if (oldArtifact is null && newArtifact is not null)
            {
                changes.Add(CreateChange(ChangeType.Added, null, newArtifact, []));
                continue;
            }

            if (oldArtifact is not null && newArtifact is null)
            {
                changes.Add(CreateChange(ChangeType.Removed, oldArtifact, null, []));
                continue;
            }

            if (oldArtifact is null || newArtifact is null)
            {
                continue;
            }

            List<PropertyChange> propertyChanges = CompareProperties(
                oldArtifact.Properties,
                newArtifact.Properties);

            if (propertyChanges.Count > 0)
            {
                changes.Add(CreateChange(
                    ChangeType.Modified,
                    oldArtifact,
                    newArtifact,
                    propertyChanges));
            }
        }

        IReadOnlyList<SystemChange> visibleChanges =
            _noiseFilterEngine.Apply(changes, noiseMode, out int hiddenCount);

        return new ComparisonResult
        {
            BeforeSnapshotId = before.Id,
            AfterSnapshotId = after.Id,
            NoiseMode = noiseMode,
            Changes = [.. visibleChanges],
            HiddenAsNoise = hiddenCount
        };
    }

    private SystemChange CreateChange(
        ChangeType changeType,
        SystemArtifact? before,
        SystemArtifact? after,
        List<PropertyChange> propertyChanges)
    {
        SystemArtifact artifact = after ?? before
            ?? throw new InvalidOperationException("Изменение не содержит системный объект.");

        (Severity severity, string explanation, string whyThisMatters) =
            _severityEngine.Evaluate(changeType, before, after, propertyChanges);

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (before is not null)
        {
            tags.UnionWith(before.Tags);
        }

        if (after is not null)
        {
            tags.UnionWith(after.Tags);
        }

        return new SystemChange
        {
            ChangeType = changeType,
            ProviderId = artifact.ProviderId,
            ArtifactType = artifact.ArtifactType,
            Identity = artifact.Identity,
            DisplayName = artifact.DisplayName,
            Before = before,
            After = after,
            ChangedProperties = propertyChanges,
            Severity = severity,
            Explanation = explanation,
            WhyThisMatters = whyThisMatters,
            Tags = tags
        };
    }

    private static List<PropertyChange> CompareProperties(
        IReadOnlyDictionary<string, ArtifactValue> before,
        IReadOnlyDictionary<string, ArtifactValue> after)
    {
        var result = new List<PropertyChange>();

        foreach (string key in before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase))
        {
            before.TryGetValue(key, out ArtifactValue? oldValue);
            after.TryGetValue(key, out ArtifactValue? newValue);

            if (!AreEqual(oldValue, newValue))
            {
                result.Add(new PropertyChange
                {
                    Name = key,
                    Before = oldValue,
                    After = newValue
                });
            }
        }

        return result;
    }

    private static bool AreEqual(ArtifactValue? left, ArtifactValue? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(left.Value, right.Value, StringComparison.Ordinal)
            && string.Equals(left.Type, right.Type, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Hash, right.Hash, StringComparison.OrdinalIgnoreCase)
            && left.Redacted == right.Redacted;
    }
}
