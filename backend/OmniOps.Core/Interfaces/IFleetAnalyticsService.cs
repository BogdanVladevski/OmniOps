using OmniOps.Core.Domain;

namespace OmniOps.Core.Interfaces;

public interface IFleetAnalyticsService
{
    Task<FleetAnalyticsResult> GetFleetAnalyticsAsync(
        Guid fleetId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DriverAnalyticsResult>> GetDriverAnalyticsAsync(
        Guid fleetId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<OperationalAnalyticsResult> GetOperationalAnalyticsAsync(
        Guid fleetId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
