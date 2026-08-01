using SysDiff.Domain;

namespace SysDiff.Core;

public sealed class DriftRiskEngine
{
    private static readonly IReadOnlyDictionary<Severity, double> Weights =
        new Dictionary<Severity, double>
        {
            [Severity.Info] = 0.5,
            [Severity.Low] = 2,
            [Severity.Medium] = 6,
            [Severity.High] = 15,
            [Severity.Critical] = 30
        };

    public DriftRiskSummary Evaluate(ComparisonResult comparison, bool partialData = false)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        Dictionary<Severity, int> counts = Enum.GetValues<Severity>()
            .ToDictionary(
                severity => severity,
                severity => comparison.Changes.Count(change => change.Severity == severity));

        double weighted = comparison.Changes.Sum(change =>
        {
            double confidence = Math.Clamp(change.Confidence, 0.25, 1.0);
            double noiseMultiplier = change.IsNoise ? 0.25 : 1.0;
            return Weights[change.Severity] * confidence * noiseMultiplier;
        });

        int providerCount = comparison.Changes
            .Select(change => change.ProviderId)
            .Where(provider => !string.IsNullOrWhiteSpace(provider))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        double diversityBonus = Math.Min(10, Math.Max(0, providerCount - 1) * 1.5);
        int score = Math.Clamp(
            (int)Math.Round(weighted + diversityBonus, MidpointRounding.AwayFromZero),
            0,
            100);

        DriftLevel level = score switch
        {
            <= 4 => DriftLevel.Stable,
            <= 14 => DriftLevel.Notice,
            <= 34 => DriftLevel.Elevated,
            <= 64 => DriftLevel.High,
            _ => DriftLevel.Critical
        };

        var factors = new List<string>();
        AddFactor(factors, counts, Severity.Critical, "критических изменений");
        AddFactor(factors, counts, Severity.High, "изменений высокой важности");
        AddFactor(factors, counts, Severity.Medium, "изменений средней важности");
        if (providerCount > 1)
        {
            factors.Add($"Изменения затрагивают {providerCount:N0} источника(ов) данных.");
        }
        if (comparison.HiddenAsNoise > 0)
        {
            factors.Add($"Фильтр шума скрыл {comparison.HiddenAsNoise:N0} изменение(й).");
        }
        if (partialData)
        {
            factors.Add("Один из снимков частичный: итоговый индекс может быть занижен.");
        }
        if (factors.Count == 0)
        {
            factors.Add("Значимых изменений относительно baseline не обнаружено.");
        }

        return new DriftRiskSummary
        {
            Score = score,
            Level = level,
            TotalChanges = comparison.Changes.Count,
            SeverityCounts = counts,
            Factors = factors,
            PartialData = partialData
        };
    }

    private static void AddFactor(
        ICollection<string> factors,
        IReadOnlyDictionary<Severity, int> counts,
        Severity severity,
        string label)
    {
        int count = counts[severity];
        if (count > 0)
        {
            factors.Add($"Обнаружено {count:N0} {label}.");
        }
    }
}
