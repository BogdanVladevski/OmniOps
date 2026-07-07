using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Domain;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class FleetAnalyticsService : IFleetAnalyticsService
{
    private readonly AppDbContext _context;
    private readonly ITelemetryRepository _telemetryRepository;

    public FleetAnalyticsService(AppDbContext context, ITelemetryRepository telemetryRepository)
    {
        _context = context;
        _telemetryRepository = telemetryRepository;
    }

    public async Task<FleetAnalyticsResult> GetFleetAnalyticsAsync(
        Guid fleetId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var vehicles = await _context.Vehicles.AsNoTracking()
            .Where(v => v.FleetId == fleetId).ToListAsync(cancellationToken);

        var externalIds = vehicles.Select(v => v.ExternalId).ToList();
        double totalDistance = 0, totalSpeed = 0;
        int speedSamples = 0, incidentCount = 0;

        foreach (var id in externalIds)
        {
            var points = await _telemetryRepository.GetByVehicleInTimeRangeAsync(id, fromUtc, toUtc, cancellationToken);
            for (var i = 1; i < points.Count; i++)
            {
                totalDistance += HaversineKm(
                    points[i - 1].Latitude, points[i - 1].Longitude,
                    points[i].Latitude, points[i].Longitude);
            }
            totalSpeed += points.Sum(p => p.Speed);
            speedSamples += points.Count;
        }

        incidentCount = await _context.Incidents.CountAsync(
            i => i.FleetId == fleetId && i.DetectedAtUtc >= fromUtc && i.DetectedAtUtc <= toUtc,
            cancellationToken);

        return new FleetAnalyticsResult(
            fleetId,
            vehicles.Count,
            Math.Round(totalDistance, 1),
            speedSamples > 0 ? Math.Round(totalSpeed / speedSamples, 1) : 0,
            incidentCount,
            vehicles.Count(v => v.Status == VehicleOperationalStatus.Active));
    }

    public async Task<IReadOnlyList<DriverAnalyticsResult>> GetDriverAnalyticsAsync(
        Guid fleetId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var drivers = await _context.Drivers.AsNoTracking()
            .Where(d => d.FleetId == fleetId).ToListAsync(cancellationToken);

        var results = new List<DriverAnalyticsResult>();
        foreach (var driver in drivers)
        {
            var tripCount = await _context.Trips.CountAsync(
                t => t.DriverId == driver.Id && t.StartedAtUtc >= fromUtc && t.StartedAtUtc <= toUtc,
                cancellationToken);
            results.Add(new DriverAnalyticsResult(driver.Id, driver.FullName, driver.SafetyScore, tripCount));
        }
        return results;
    }

    public async Task<OperationalAnalyticsResult> GetOperationalAnalyticsAsync(
        Guid fleetId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var activeTrips = await _context.Trips.CountAsync(
            t => t.Vehicle.FleetId == fleetId && t.Status == TripStatus.InProgress, cancellationToken);
        var completedTrips = await _context.Trips.CountAsync(
            t => t.Vehicle.FleetId == fleetId && t.Status == TripStatus.Completed
                && t.CompletedAtUtc >= fromUtc && t.CompletedAtUtc <= toUtc, cancellationToken);
        var openIncidents = await _context.Incidents.CountAsync(
            i => i.FleetId == fleetId && i.Status == IncidentStatus.Open, cancellationToken);
        var depotUtilization = await _context.Depots.AsNoTracking()
            .Where(d => d.FleetId == fleetId).SumAsync(d => d.Capacity, cancellationToken);

        return new OperationalAnalyticsResult(fleetId, activeTrips, completedTrips, openIncidents, depotUtilization);
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
