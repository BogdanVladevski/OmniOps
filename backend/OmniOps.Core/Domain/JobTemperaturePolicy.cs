using OmniOps.Core.Entities;
using OmniOps.Core.Enums;

namespace OmniOps.Core.Domain;

/// <summary>
/// Temperature log rules for refrigerated jobs (a job with a safe range). Readings are entered by the driver
/// from the fridge unit's display with a photo as evidence.
/// </summary>
public static class JobTemperaturePolicy
{
    /// <summary>How often a reading is expected while the load is on the road.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(2);

    /// <summary>Delivery needs a reading at least this recent, so the log shows the state at hand-over.</summary>
    public static readonly TimeSpan DeliveryReadingMaxAge = TimeSpan.FromMinutes(30);

    public const double MinPlausibleCelsius = -40;
    public const double MaxPlausibleCelsius = 40;

    public static bool IsRefrigerated(Trip job) => job.MinTempCelsius.HasValue && job.MaxTempCelsius.HasValue;

    public static bool IsOutOfRange(Trip job, double celsius) =>
        IsRefrigerated(job) && (celsius < job.MinTempCelsius!.Value || celsius > job.MaxTempCelsius!.Value);

    public static IEnumerable<TripDocument> Readings(Trip job) =>
        job.Documents.Where(d => d.Kind == TripDocumentKind.TemperaturePhoto && d.TemperatureCelsius.HasValue)
            .OrderBy(d => d.CapturedAtUtc);

    /// <summary>When the next reading is due while on the way; null if not refrigerated or not running.</summary>
    public static DateTime? NextCheckDueUtc(Trip job)
    {
        if (!IsRefrigerated(job) || job.Status != TripStatus.InProgress)
        {
            return null;
        }

        var last = Readings(job).LastOrDefault()?.CapturedAtUtc;
        // Before any reading, one is due as soon as the trip starts (the pickup check).
        return last is { } at ? at + CheckInterval : job.StartedAtUtc ?? job.CreatedAtUtc;
    }

    public static bool HasRecentReading(Trip job, DateTime nowUtc) =>
        Readings(job).Any(d => nowUtc - d.CapturedAtUtc <= DeliveryReadingMaxAge);
}
