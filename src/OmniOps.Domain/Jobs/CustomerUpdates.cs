namespace OmniOps.Domain.Jobs;

/// <summary>An email update the customer can receive about a delivery, in the order they happen.</summary>
public enum CustomerUpdate
{
    PickedUp = 0,
    ArrivingSoon = 1,
    Delivered = 2,
}

/// <summary>
/// Which update a job is due for. At most one is sent at a time, always the latest that applies: a customer never gets
/// "picked up" after "arriving soon", and an update that is no longer news (a delivery from yesterday) is not sent.
/// </summary>
public static class CustomerUpdates
{
    /// <summary>"Arriving soon" goes out when the estimate is this close.</summary>
    public static readonly TimeSpan ArrivingWithin = TimeSpan.FromMinutes(30);

    /// <summary>A position older than this is too stale to promise an arrival time.</summary>
    public static readonly TimeSpan FreshPosition = TimeSpan.FromMinutes(10);

    /// <summary>Updates older than this are no longer news.</summary>
    public static readonly TimeSpan PickedUpStillNews = TimeSpan.FromHours(6);
    public static readonly TimeSpan DeliveredStillNews = TimeSpan.FromHours(24);

    public static CustomerUpdate? Next(Job job, DateTime nowUtc)
    {
        if (job.Status == JobStatus.Delivered)
        {
            return job.CustomerDeliveredNoticeAtUtc is null && job.DeliveredAtUtc is { } at && nowUtc - at <= DeliveredStillNews
                ? CustomerUpdate.Delivered
                : null;
        }

        if (job.Status != JobStatus.InProgress)
        {
            return null;
        }

        if (job.CustomerArrivingNoticeAtUtc is null && IsArrivingSoon(job, nowUtc))
        {
            return CustomerUpdate.ArrivingSoon;
        }

        return job.CustomerPickedUpNoticeAtUtc is null
               && job.CustomerArrivingNoticeAtUtc is null
               && job.StartedAtUtc is { } started && nowUtc - started <= PickedUpStillNews
            ? CustomerUpdate.PickedUp
            : null;
    }

    /// <summary>Records that <paramref name="update"/> went out, and that the earlier ones no longer need to.</summary>
    public static void MarkHandled(Job job, CustomerUpdate update, DateTime nowUtc)
    {
        job.CustomerPickedUpNoticeAtUtc ??= nowUtc;
        if (update >= CustomerUpdate.ArrivingSoon)
        {
            job.CustomerArrivingNoticeAtUtc ??= nowUtc;
        }

        if (update == CustomerUpdate.Delivered)
        {
            job.CustomerDeliveredNoticeAtUtc ??= nowUtc;
        }
    }

    private static bool IsArrivingSoon(Job job, DateTime nowUtc)
    {
        // Already at the gate: "arriving soon" is no longer news.
        if (job.DeliveryArrivedAtUtc is not null)
        {
            return false;
        }

        if (job is not { LastLatitude: { } lat, LastLongitude: { } lon, LastPositionAtUtc: { } at, DestinationLatitude: { } toLat, DestinationLongitude: { } toLon }
            || nowUtc - at > FreshPosition)
        {
            return false;
        }

        var (km, eta) = DeliveryEta.Estimate(lat, lon, toLat, toLon, at);
        return km > 0 && eta - nowUtc <= ArrivingWithin;
    }
}
