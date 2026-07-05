using OmniOps.Core.Enums;

namespace OmniOps.Core.Domain;

/// <param name="IsAnomaly">True for both Warning and Critical — caller should check Severity for escalation logic.</param>
/// <param name="Severity">
/// Warning = trending toward breach but still in range.
/// Critical = outside safe range, or has been for >60s.
/// Null when IsAnomaly is false.
/// </param>
/// <param name="ExcursionDurationSeconds">
/// Consecutive seconds outside the safe range. Zero when in range or no active shipment.
/// </param>
public record AnomalyAnalysis(
    bool IsAnomaly,
    AnomalySeverity? Severity = null,
    int ExcursionDurationSeconds = 0);
