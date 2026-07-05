namespace OmniOps.Core.Enums;

public enum AnomalySeverity
{
    // Temp is moving fast toward a breach but hasn't crossed it yet.
    Warning,
    // Actually outside the safe range, or has been for a while.
    Critical
}
