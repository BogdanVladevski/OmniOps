using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

/// <summary>
/// Live updates to connected apps. Everything goes to the owning organization's group only:
/// never broadcast without an organization id.
/// </summary>
public interface ITelemetryBroadcastService
{
    Task BroadcastTelemetryUpdateAsync(Guid organizationId, VehicleTelemetry telemetry, CancellationToken cancellationToken = default);

    Task BroadcastAlertAsync(Guid organizationId, string vehicleId, string alertType, string title, string message, CancellationToken cancellationToken = default);
}
