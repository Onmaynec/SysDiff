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
        NoiseMode noiseMode,
        bool crossMachine = false)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        bool differentMachines = !string.IsNullOrWhiteSpace(before.MachineFingerprint)
            && !string.IsNullOrWhiteSpace(after.MachineFingerprint)
            && !string.Equals(
                before.MachineFingerprint,
                after.MachineFingerprint,
                StringComparison.OrdinalIgnoreCase);

        var warnings = new List<string>();
        if (differentMachines && !crossMachine)
        {
            warnings.Add(
                "Снимки получены на разных компьютерах. Используйте --cross-machine для явного межмашинного сравнения.");
        }
        else if (differentMachines)
        {
            warnings.Add(
                "Включено межмашинное сравнение: confidence изменений снижен, версии Windows и архитектура требуют ручной проверки.");
        }

        if (!string.Equals(before.WindowsBuild, after.WindowsBuild, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Windows build различается: {before.WindowsBuild ?? "unknown"} → {after.WindowsBuild ?? "unknown"}.");
        }

        if (!string.Equals(before.Architecture, after.Architecture, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Архитектура различается: {before.Architecture} → {after.Architecture}.");
        }

        var beforeMap = before.Artifacts.ToDictionary(
            x => x.Identity,
            StringComparer.OrdinalIgnoreCase);
        var afterMap = after.Artifacts.ToDictionary(
            x => x.Identity,
            StringComparer.OrdinalIgnoreCase);

        var changes = new List<SystemChange>();
        double baseConfidence = differentMachines && crossMachine ? 0.75 : 1.0;

        foreach (string identity in beforeMap.Keys.Union(afterMap.Keys, StringComparer.OrdinalIgnoreCase))
        {
            beforeMap.TryGetValue(identity, out SystemArtifact? oldArtifact);
            afterMap.TryGetValue(identity, out SystemArtifact? newArtifact);

            if (oldArtifact is null && newArtifact is not null)
            {
                changes.Add(CreateChange(ChangeType.Added, null, newArtifact, [], baseConfidence));
                continue;
            }

            if (oldArtifact is not null && newArtifact is null)
            {
                changes.Add(CreateChange(ChangeType.Removed, oldArtifact, null, [], baseConfidence));
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
                    propertyChanges,
                    baseConfidence));
            }
        }

        DetectFileMoves(changes, baseConfidence);

        IReadOnlyList<SystemChange> visibleChanges =
            _noiseFilterEngine.Apply(changes, noiseMode, out int hiddenCount);

        return new ComparisonResult
        {
            BeforeSnapshotId = before.Id,
            AfterSnapshotId = after.Id,
            NoiseMode = noiseMode,
            CrossMachine = differentMachines && crossMachine,
            Warnings = warnings,
            Changes = [.. visibleChanges],
            HiddenAsNoise = hiddenCount
        };
    }

    private void DetectFileMoves(List<SystemChange> changes, double baseConfidence)
    {
        SystemChange[] removed = changes
            .Where(x => x.ChangeType == ChangeType.Removed
                && x.ProviderId.Equals("filesystem", StringComparison.OrdinalIgnoreCase)
                && x.ArtifactType.Equals("File", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        SystemChange[] added = changes
            .Where(x => x.ChangeType == ChangeType.Added
                && x.ProviderId.Equals("filesystem", StringComparison.OrdinalIgnoreCase)
                && x.ArtifactType.Equals("File", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var removedGroups = removed
            .Select(x => (Change: x, Key: FileMatchKey(x.Before)))
            .Where(x => x.Key is not null)
            .GroupBy(x => x.Key!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Change).ToArray(), StringComparer.OrdinalIgnoreCase);
        var addedGroups = added
            .Select(x => (Change: x, Key: FileMatchKey(x.After)))
            .Where(x => x.Key is not null)
            .GroupBy(x => x.Key!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Change).ToArray(), StringComparer.OrdinalIgnoreCase);

        foreach ((string key, SystemChange[] oldCandidates) in removedGroups)
        {
            if (oldCandidates.Length != 1
                || !addedGroups.TryGetValue(key, out SystemChange[]? newCandidates)
                || newCandidates.Length != 1)
            {
                continue;
            }

            SystemChange removedChange = oldCandidates[0];
            SystemChange addedChange = newCandidates[0];
            SystemArtifact oldArtifact = removedChange.Before!;
            SystemArtifact newArtifact = addedChange.After!;
            string oldPath = GetValue(oldArtifact, "Path") ?? oldArtifact.DisplayName;
            string newPath = GetValue(newArtifact, "Path") ?? newArtifact.DisplayName;
            string? oldDirectory = Path.GetDirectoryName(oldPath);
            string? newDirectory = Path.GetDirectoryName(newPath);
            ChangeType type = string.Equals(
                oldDirectory,
                newDirectory,
                StringComparison.OrdinalIgnoreCase)
                ? ChangeType.Renamed
                : ChangeType.Moved;

            changes.Remove(removedChange);
            changes.Remove(addedChange);
            changes.Add(CreateChange(
                type,
                oldArtifact,
                newArtifact,
                [
                    new PropertyChange
                    {
                        Name = "Path",
                        Before = ArtifactValue.From(oldPath),
                        After = ArtifactValue.From(newPath)
                    }
                ],
                Math.Min(baseConfidence, 0.95)));
        }
    }

    private SystemChange CreateChange(
        ChangeType changeType,
        SystemArtifact? before,
        SystemArtifact? after,
        List<PropertyChange> propertyChanges,
        double confidence)
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

        if (changeType is ChangeType.Moved or ChangeType.Renamed)
        {
            tags.Add("HeuristicMatch");
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
            Tags = tags,
            Confidence = confidence
        };
    }

    private static string? FileMatchKey(SystemArtifact? artifact)
    {
        if (artifact is null)
        {
            return null;
        }

        string? hash = GetValue(artifact, "Sha256");
        string? size = GetValue(artifact, "Size");
        return string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(size)
            ? null
            : $"{hash}|{size}";
    }

    private static string? GetValue(SystemArtifact artifact, string name) =>
        artifact.Properties.TryGetValue(name, out ArtifactValue? value)
            ? value.Value
            : null;

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
