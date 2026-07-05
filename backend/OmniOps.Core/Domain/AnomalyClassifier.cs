using OmniOps.Core.Enums;

namespace OmniOps.Core.Domain;

/// <summary>
/// Pure functions for excursion severity classification and trend detection.
/// No Redis, no I/O — split out so we can unit test the math without spinning up infrastructure.
/// </summary>
public static class AnomalyClassifier
{
    public const int CriticalExcursionThresholdSeconds = 60;
    public const double TrendRateWarningThreshold = 1.5;
    public const int MinReadingsForTrend = 5;

    /// <summary>
    /// Classifies a hard excursion (temp already outside safe range).
    /// Warning = just started, Critical = sustained past the threshold.
    /// </summary>
    public static AnomalySeverity ClassifyExcursion(int excursionDurationSeconds) =>
        excursionDurationSeconds >= CriticalExcursionThresholdSeconds
            ? AnomalySeverity.Critical
            : AnomalySeverity.Warning;

    /// <summary>
    /// Checks whether the temperature is trending fast enough toward a safe-range boundary
    /// that we should pre-warn before the breach actually happens.
    /// Returns true only if we have enough history to say something meaningful.
    /// </summary>
    public static bool IsTrendingTowardBreach(
        IReadOnlyList<double> recentTemps,
        double minSafe,
        double maxSafe)
    {
        if (recentTemps.Count < MinReadingsForTrend)
            return false;

        var latest = recentTemps[^1];
        var oldest = recentTemps[0];

        // Still outside range already — caller should use ClassifyExcursion instead.
        if (latest > maxSafe || latest < minSafe)
            return false;

        var ratePerReading = (latest - oldest) / (recentTemps.Count - 1);

        if (Math.Abs(ratePerReading) < TrendRateWarningThreshold)
            return false;

        // Project ~10 readings ahead at the current rate.
        var projected = latest + ratePerReading * 10;
        return projected > maxSafe || projected < minSafe;
    }
}
