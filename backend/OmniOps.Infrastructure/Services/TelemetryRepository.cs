using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class TelemetryRepository : ITelemetryRepository
{
    private readonly AppDbContext _context;

    public TelemetryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(VehicleTelemetry telemetry, CancellationToken cancellationToken = default)
    {
        await _context.Telemetries.AddAsync(telemetry, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VehicleTelemetry>> GetByVehicleInTimeRangeAsync(
        string vehicleId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return await _context.Telemetries
            .AsNoTracking()
            .Where(t => t.VehicleId == vehicleId && t.Timestamp >= fromUtc && t.Timestamp <= toUtc)
            .OrderBy(t => t.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TelemetryAggregationBucket>> GetAggregationsAsync(
        string vehicleId,
        DateTime fromUtc,
        DateTime toUtc,
        int bucketMinutes,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.Telemetries
            .AsNoTracking()
            .Where(t => t.VehicleId == vehicleId && t.Timestamp >= fromUtc && t.Timestamp <= toUtc)
            .Select(t => new { t.Timestamp, t.Speed, t.EngineTemperature })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(t => TruncateToBucket(t.Timestamp, bucketMinutes))
            .OrderBy(g => g.Key)
            .Select(g => new TelemetryAggregationBucket(
                g.Key,
                g.Average(x => x.Speed),
                g.Average(x => x.EngineTemperature),
                g.Min(x => x.EngineTemperature),
                g.Max(x => x.EngineTemperature),
                g.Count()))
            .ToList();
    }

    public async Task<IReadOnlyList<HeatmapBucket>> GetHeatmapBucketsAsync(
        IReadOnlyList<string> vehicleIds,
        DateTime fromUtc,
        DateTime toUtc,
        double gridSizeDegrees,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.Telemetries
            .AsNoTracking()
            .Where(t => vehicleIds.Contains(t.VehicleId)
                && t.Timestamp >= fromUtc
                && t.Timestamp <= toUtc)
            .Select(t => new { t.Latitude, t.Longitude, t.Speed })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(t => (
                Math.Floor(t.Latitude / gridSizeDegrees) * gridSizeDegrees,
                Math.Floor(t.Longitude / gridSizeDegrees) * gridSizeDegrees))
            .Select(g => new HeatmapBucket(
                g.Key.Item1 + gridSizeDegrees / 2,
                g.Key.Item2 + gridSizeDegrees / 2,
                g.Average(x => x.Speed),
                g.Count()))
            .ToList();
    }

    private static DateTime TruncateToBucket(DateTime timestamp, int bucketMinutes)
    {
        var minute = timestamp.Minute - timestamp.Minute % bucketMinutes;
        return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, minute, 0, DateTimeKind.Utc);
    }
}
