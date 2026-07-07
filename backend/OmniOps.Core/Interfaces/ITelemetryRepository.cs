using OmniOps.Core.Domain;
using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface ITelemetryRepository
{
    Task AddAsync(VehicleTelemetry telemetry, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleTelemetry>> GetByVehicleInTimeRangeAsync(
        string vehicleId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TelemetryAggregationBucket>> GetAggregationsAsync(
        string vehicleId,
        DateTime fromUtc,
        DateTime toUtc,
        int bucketMinutes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HeatmapBucket>> GetHeatmapBucketsAsync(
        IReadOnlyList<string> vehicleIds,
        DateTime fromUtc,
        DateTime toUtc,
        double gridSizeDegrees,
        CancellationToken cancellationToken = default);
}
