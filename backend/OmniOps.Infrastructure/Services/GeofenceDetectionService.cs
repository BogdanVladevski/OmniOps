using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Events;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

public class GeofenceDetectionService : IGeofenceDetectionService
{
    private readonly IGeofenceRepository _geofenceRepository;
    private readonly IDistributedCache _cache;

    public GeofenceDetectionService(IGeofenceRepository geofenceRepository, IDistributedCache cache)
    {
        _geofenceRepository = geofenceRepository;
        _cache = cache;
    }

    public async Task<IReadOnlyList<GeofenceBreachedEvent>> DetectBreachesAsync(
        VehicleTelemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        var geofences = await _geofenceRepository.GetActiveAsync(cancellationToken: cancellationToken);
        var breaches = new List<GeofenceBreachedEvent>();

        foreach (var geofence in geofences)
        {
            var inside = IsInside(geofence, telemetry.Latitude, telemetry.Longitude);
            var cacheKey = $"geofence:state:{telemetry.VehicleId}:{geofence.Id}";
            var previous = await _cache.GetStringAsync(cacheKey, cancellationToken);
            var wasInside = previous == "1";

            if (previous is null && inside)
            {
                await _cache.SetStringAsync(cacheKey, "1", cancellationToken);
                breaches.Add(new GeofenceBreachedEvent(geofence.Id, telemetry.VehicleId, GeofenceBreachType.Entry));
            }
            else if (previous is not null && wasInside != inside)
            {
                await _cache.SetStringAsync(cacheKey, inside ? "1" : "0", cancellationToken);
                breaches.Add(new GeofenceBreachedEvent(
                    geofence.Id,
                    telemetry.VehicleId,
                    inside ? GeofenceBreachType.Entry : GeofenceBreachType.Exit));
            }
            else if (previous is null)
            {
                await _cache.SetStringAsync(cacheKey, inside ? "1" : "0", cancellationToken);
            }
        }

        return breaches;
    }

    public static bool IsInside(Geofence geofence, double latitude, double longitude) =>
        geofence.ShapeType switch
        {
            GeofenceShapeType.Radius => IsInsideRadius(
                geofence.CenterLatitude!.Value,
                geofence.CenterLongitude!.Value,
                geofence.RadiusMeters!.Value,
                latitude,
                longitude),
            GeofenceShapeType.Polygon => IsInsidePolygon(
                ParsePolygon(geofence.PolygonCoordinatesJson),
                latitude,
                longitude),
            _ => false
        };

    private static bool IsInsideRadius(
        double centerLat, double centerLon, double radiusMeters, double lat, double lon)
    {
        var distance = HaversineMeters(centerLat, centerLon, lat, lon);
        return distance <= radiusMeters;
    }

    private static IReadOnlyList<(double Lat, double Lon)> ParsePolygon(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<(double, double)>();
        }

        var points = JsonSerializer.Deserialize<List<double[]>>(json) ?? [];
        return points.Where(p => p.Length >= 2).Select(p => (p[0], p[1])).ToList();
    }

    /// <summary>Ray-casting point-in-polygon test.</summary>
    private static bool IsInsidePolygon(IReadOnlyList<(double Lat, double Lon)> polygon, double lat, double lon)
    {
        if (polygon.Count < 3)
        {
            return false;
        }

        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var (yi, xi) = polygon[i];
            var (yj, xj) = polygon[j];
            var intersect = yi > lat != yj > lat
                && lon < (xj - xi) * (lat - yi) / (yj - yi + double.Epsilon) + xi;
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6371000;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
