namespace OmniOps.Core.Domain;

/// <summary>
/// Rough arrival estimate from the truck's last position, without a routing service:
/// straight-line distance × a road detour factor at a typical truck average speed.
/// Shown to customers as an estimate, never as a promise.
/// </summary>
public static class DeliveryEta
{
    public const double RoadDetourFactor = 1.3;
    public const double AverageTruckSpeedKmh = 60;

    /// <summary>Within this distance the truck is treated as arriving now.</summary>
    public const double ArrivedRadiusKm = 0.3;

    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>Estimated road distance (km) and arrival time from a position reported at <paramref name="positionAtUtc"/>.</summary>
    public static (double RoadDistanceKm, DateTime EtaUtc) Estimate(
        double fromLat, double fromLon, double toLat, double toLon, DateTime positionAtUtc)
    {
        var straight = DistanceKm(fromLat, fromLon, toLat, toLon);
        if (straight <= ArrivedRadiusKm)
        {
            return (0, positionAtUtc);
        }

        var road = straight * RoadDetourFactor;
        return (Math.Round(road, 1), positionAtUtc.AddHours(road / AverageTruckSpeedKmh));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
