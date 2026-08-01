using SysDiff.Core;
using SysDiff.Domain;

namespace SysDiff.Core.Tests;

public sealed class DriftRiskEngineTests
{
    private readonly DriftRiskEngine _engine = new();

    [Fact]
    public void EmptyComparison_IsStable()
    {
        DriftRiskSummary result = _engine.Evaluate(CreateComparison());

        Assert.Equal(0, result.Score);
        Assert.Equal(DriftLevel.Stable, result.Level);
        Assert.Equal(0, result.TotalChanges);
        Assert.Contains(result.Factors, value => value.Contains("не обнаружено", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CriticalChanges_ProduceCriticalRisk()
    {
        ComparisonResult comparison = CreateComparison(
            Enumerable.Range(0, 4).Select(index => CreateChange(
                Severity.Critical,
                $"provider-{index}",
                confidence: 1.0)).ToArray());

        DriftRiskSummary result = _engine.Evaluate(comparison);

        Assert.Equal(100, result.Score);
        Assert.Equal(DriftLevel.Critical, result.Level);
        Assert.Equal(4, result.SeverityCounts[Severity.Critical]);
    }

    [Fact]
    public void LowConfidenceAndNoise_ReduceScore()
    {
        ComparisonResult clean = CreateComparison(CreateChange(Severity.High, "registry", 1.0, false));
        ComparisonResult noisy = CreateComparison(CreateChange(Severity.High, "registry", 0.25, true));

        DriftRiskSummary cleanResult = _engine.Evaluate(clean);
        DriftRiskSummary noisyResult = _engine.Evaluate(noisy);

        Assert.True(cleanResult.Score > noisyResult.Score);
    }

    [Fact]
    public void PartialData_AddsWarningWithoutChangingDeterminism()
    {
        ComparisonResult comparison = CreateComparison(CreateChange(Severity.Medium, "filesystem"));

        DriftRiskSummary first = _engine.Evaluate(comparison, partialData: true);
        DriftRiskSummary second = _engine.Evaluate(comparison, partialData: true);

        Assert.Equal(first.Score, second.Score);
        Assert.True(first.PartialData);
        Assert.Contains(first.Factors, value => value.Contains("частич", StringComparison.OrdinalIgnoreCase));
    }

    private static ComparisonResult CreateComparison(params SystemChange[] changes) =>
        new()
        {
            BeforeSnapshotId = Guid.NewGuid(),
            AfterSnapshotId = Guid.NewGuid(),
            Changes = changes.ToList()
        };

    private static SystemChange CreateChange(
        Severity severity,
        string provider,
        double confidence = 1.0,
        bool isNoise = false) =>
        new()
        {
            ChangeType = ChangeType.Modified,
            ProviderId = provider,
            ArtifactType = "Test",
            Identity = Guid.NewGuid().ToString("N"),
            DisplayName = "Test change",
            Severity = severity,
            Confidence = confidence,
            IsNoise = isNoise
        };
}
