using OmniOps.Core.Domain;
using OmniOps.Core.Enums;

namespace OmniOps.Application.Tests.Domain;

public class AnomalyClassifierTests
{
    // ── ClassifyExcursion ──────────────────────────────────────────────────────

    [Fact]
    public void ClassifyExcursion_JustStarted_IsWarning()
    {
        var severity = AnomalyClassifier.ClassifyExcursion(0);
        Assert.Equal(AnomalySeverity.Warning, severity);
    }

    [Fact]
    public void ClassifyExcursion_BelowThreshold_StaysWarning()
    {
        var severity = AnomalyClassifier.ClassifyExcursion(
            AnomalyClassifier.CriticalExcursionThresholdSeconds - 1);
        Assert.Equal(AnomalySeverity.Warning, severity);
    }

    [Fact]
    public void ClassifyExcursion_AtThreshold_EscalatesToCritical()
    {
        var severity = AnomalyClassifier.ClassifyExcursion(
            AnomalyClassifier.CriticalExcursionThresholdSeconds);
        Assert.Equal(AnomalySeverity.Critical, severity);
    }

    [Fact]
    public void ClassifyExcursion_WellPastThreshold_IsCritical()
    {
        var severity = AnomalyClassifier.ClassifyExcursion(300);
        Assert.Equal(AnomalySeverity.Critical, severity);
    }

    // ── IsTrendingTowardBreach ─────────────────────────────────────────────────

    [Fact]
    public void IsTrendingTowardBreach_NotEnoughHistory_ReturnsFalse()
    {
        // Need at least MinReadingsForTrend points before we say anything.
        var temps = Enumerable.Range(0, AnomalyClassifier.MinReadingsForTrend - 1)
            .Select(_ => 70.0)
            .ToList();

        Assert.False(AnomalyClassifier.IsTrendingTowardBreach(temps, 50, 100));
    }

    [Fact]
    public void IsTrendingTowardBreach_StableNearEdge_NoFalsePositive()
    {
        // 10 readings hovering at 98°C — dangerously close to 100°C limit but not moving.
        var temps = Enumerable.Repeat(98.0, 10).ToList();

        Assert.False(AnomalyClassifier.IsTrendingTowardBreach(temps, 50, 100));
    }

    [Fact]
    public void IsTrendingTowardBreach_FastRiseProjectingOutOfRange_IsTrue()
    {
        // Rising 2°C per reading from 75°C — will blow past 100°C within the projection window.
        var temps = Enumerable.Range(0, 10)
            .Select(i => 75.0 + i * 2.0)
            .ToList();

        Assert.True(AnomalyClassifier.IsTrendingTowardBreach(temps, 50, 100));
    }

    [Fact]
    public void IsTrendingTowardBreach_FastRiseBothDirections_DetectedForFall()
    {
        // Dropping fast toward the lower safe boundary.
        var temps = Enumerable.Range(0, 10)
            .Select(i => 75.0 - i * 2.5)
            .ToList();

        Assert.True(AnomalyClassifier.IsTrendingTowardBreach(temps, 50, 100));
    }

    [Fact]
    public void IsTrendingTowardBreach_FastRiseButWontBreach_ReturnsFalse()
    {
        // Rising at 1°C/reading from 80°C — fast-ish but the projection still lands inside range.
        var temps = Enumerable.Range(0, 10)
            .Select(i => 80.0 + i * 1.0)
            .ToList();

        // Projection = 89 + 10 * 1 = 99 — still under 100, no warning.
        Assert.False(AnomalyClassifier.IsTrendingTowardBreach(temps, 50, 100));
    }

    [Fact]
    public void IsTrendingTowardBreach_AlreadyOutsideRange_ReturnsFalse()
    {
        // Caller is supposed to use ClassifyExcursion for this case, not the trend check.
        var temps = Enumerable.Range(0, 10).Select(_ => 105.0).ToList();
        Assert.False(AnomalyClassifier.IsTrendingTowardBreach(temps, 50, 100));
    }

    [Fact]
    public void IsTrendingTowardBreach_SlowRiseWithinRange_NoWarning()
    {
        // Moving at 0.5°C/reading — well below the 1.5°C threshold, no early warning.
        var temps = Enumerable.Range(0, 10)
            .Select(i => 70.0 + i * 0.5)
            .ToList();

        Assert.False(AnomalyClassifier.IsTrendingTowardBreach(temps, 50, 100));
    }
}
