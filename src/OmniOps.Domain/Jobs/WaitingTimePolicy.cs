using OmniOps.Domain.Billing;

namespace OmniOps.Domain.Jobs;

/// <summary>A wait at one stop of a job, measured and (if the company charges for it) priced.</summary>
public sealed record StopWait(
    string Stop,
    DateTime ArrivedAtUtc,
    DateTime? ReleasedAtUtc,
    ArrivalSource Source,
    double? DistanceFromStopMeters,
    bool Verified,
    int WaitedMinutes,
    int FreeMinutes,
    int ChargedMinutes,
    decimal? Amount)
{
    public bool Running => ReleasedAtUtc is null;
    public DateTime ChargedFromUtc => ArrivedAtUtc.AddMinutes(FreeMinutes);
}

/// <summary>
/// Waiting time (detention) at pickup and delivery. The evidence is the phone: GPS entering the stop's area, or the
/// driver's "I've arrived" tap with the phone's position at that moment.
/// </summary>
public static class WaitingTimePolicy
{
    public const string Pickup = "pickup";
    public const string Delivery = "delivery";

    /// <summary>Within this distance of the address the truck is "at the stop" (yards and gates are large).</summary>
    public const double AtStopMeters = 250;

    /// <summary>Further than this, the truck has left again (the gap avoids flapping at the edge).</summary>
    public const double LeftStopMeters = 600;

    /// <summary>One reading this far away means the truck has certainly gone (no second reading needed).</summary>
    public const double CertainlyGoneMeters = 5000;

    /// <summary>A driver's tap this far from the address is shown but not treated as proof.</summary>
    public const double TapTrustedWithinMeters = 1000;

    /// <summary>Waiting beyond the free time is charged per started block.</summary>
    public const int BlockMinutes = 15;

    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2) =>
        DeliveryEta.DistanceKm(lat1, lon1, lat2, lon2) * 1000;

    /// <summary>
    /// Follows GPS points (oldest first) near a stop: the first point inside sets the arrival; leaving afterwards
    /// clears it (the truck came and went). Leaving needs two readings in a row away from the stop, or one far away,
    /// so a single bad GPS fix never erases a real wait. Returns the arrival after these points.
    /// </summary>
    public static DateTime? TrackArrival(
        DateTime? arrival, IEnumerable<(DateTime At, double Latitude, double Longitude)> points, double stopLatitude, double stopLongitude)
    {
        var awayInARow = 0;
        foreach (var (at, lat, lon) in points)
        {
            var distance = DistanceMeters(lat, lon, stopLatitude, stopLongitude);
            if (distance <= AtStopMeters)
            {
                awayInARow = 0;
                // A point that arrives late but was taken earlier moves the arrival back.
                arrival = arrival is { } current && current <= at ? current : at;
            }
            else if (distance > LeftStopMeters && arrival is { } since && at > since)
            {
                awayInARow++;
                if (awayInARow >= 2 || distance > CertainlyGoneMeters)
                {
                    arrival = null;
                    awayInARow = 0;
                }
            }
        }

        return arrival;
    }

    /// <summary>Minutes charged: everything past the free time, rounded up to whole blocks.</summary>
    public static int ChargedMinutes(int waitedMinutes, int freeMinutes)
    {
        var over = waitedMinutes - freeMinutes;
        return over <= 0 ? 0 : (int)Math.Ceiling(over / (double)BlockMinutes) * BlockMinutes;
    }

    public static decimal? Amount(int chargedMinutes, decimal? ratePerHour) =>
        ratePerHour is { } rate && chargedMinutes > 0 ? InvoiceRules.Round(rate * chargedMinutes / 60m) : null;

    /// <summary>The job's waits so far; a running wait is measured up to <paramref name="nowUtc"/>.</summary>
    public static IReadOnlyList<StopWait> Waits(Job job, int freeMinutes, decimal? ratePerHour, DateTime nowUtc)
    {
        var waits = new List<StopWait>();
        if (job.PickupArrivedAtUtc is { } pickupArrived)
        {
            waits.Add(Measure(Pickup, pickupArrived, job.StartedAtUtc, job.PickupArrivalSource ?? ArrivalSource.Driver,
                Distance(job.PickupArrivalLatitude, job.PickupArrivalLongitude, job.OriginLatitude, job.OriginLongitude),
                freeMinutes, ratePerHour, nowUtc, job.Status == JobStatus.Cancelled));
        }

        if (job.DeliveryArrivedAtUtc is { } deliveryArrived)
        {
            waits.Add(Measure(Delivery, deliveryArrived, job.DeliveredAtUtc, job.DeliveryArrivalSource ?? ArrivalSource.Gps,
                Distance(job.DeliveryArrivalLatitude, job.DeliveryArrivalLongitude, job.DestinationLatitude, job.DestinationLongitude),
                freeMinutes, ratePerHour, nowUtc, job.Status == JobStatus.Cancelled));
        }

        return waits;
    }

    private static double? Distance(double? lat, double? lon, double? stopLat, double? stopLon) =>
        lat is { } a && lon is { } b && stopLat is { } c && stopLon is { } d ? Math.Round(DistanceMeters(a, b, c, d)) : null;

    private static StopWait Measure(
        string stop, DateTime arrived, DateTime? released, ArrivalSource source, double? distance,
        int freeMinutes, decimal? rate, DateTime now, bool cancelled)
    {
        // A cancelled job's open wait stops counting: nothing is billed for it.
        var end = released ?? (cancelled ? arrived : now);
        var waited = Math.Max(0, (int)Math.Floor((end - arrived).TotalMinutes));
        var verified = source == ArrivalSource.Gps || (distance is { } d && d <= TapTrustedWithinMeters);
        var charged = ChargedMinutes(waited, freeMinutes);
        return new StopWait(stop, arrived, released, source, distance, verified, waited, freeMinutes, charged, Amount(charged, rate));
    }
}
