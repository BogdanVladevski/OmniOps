using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Domain.Billing;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Jobs;

/// <summary>
/// Waiting at a stop: when the truck arrived and how (GPS, or the driver's tap and how far from the address),
/// how long it waited, and what is chargeable under the company's terms.
/// </summary>
public sealed record WaitDto(
    string Stop,
    DateTime ArrivedAtUtc,
    DateTime? ReleasedAtUtc,
    string Source,
    double? DistanceFromStopMeters,
    bool Verified,
    bool Running,
    int WaitedMinutes,
    int FreeMinutes,
    DateTime ChargedFromUtc,
    int ChargedMinutes,
    decimal? Amount,
    decimal? RatePerHour,
    string Currency)
{
    public static WaitDto From(StopWait w, decimal? rate, string currency) =>
        new(w.Stop, w.ArrivedAtUtc, w.ReleasedAtUtc, w.Source.ToString(), w.DistanceFromStopMeters, w.Verified, w.Running,
            w.WaitedMinutes, w.FreeMinutes, w.ChargedFromUtc, w.ChargedMinutes, w.Amount, rate, currency);
}

/// <summary>An extra invoice line the office can add with one tap (e.g. waiting time), already priced.</summary>
public sealed record SuggestedLineDto(string Description, decimal Quantity, decimal UnitPrice);

/// <summary>A company's waiting-time terms (free time, hourly rate, time zone) applied to a job.</summary>
public sealed record WaitingTerms(int FreeMinutes, decimal? RatePerHour, string Currency, TimeZoneInfo Zone)
{
    public static readonly WaitingTerms Default = new(120, null, "EUR", TimeZoneInfo.Utc);

    public static WaitingTerms From(BillingProfile? profile) =>
        profile is null ? Default : new(profile.FreeWaitingMinutes, profile.WaitingRatePerHour, profile.Currency, ZoneOf(profile.TimeZone));

    public static async Task<WaitingTerms> ForAsync(IAppDbContext db, Guid organizationId, CancellationToken ct) =>
        From(await db.BillingProfiles.AcrossCompanies().FirstOrDefaultAsync(p => p.OrganizationId == organizationId, ct));

    public static TimeZoneInfo ZoneOf(string? id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : TimeZoneInfo.Utc;

    public IReadOnlyList<WaitDto> Waits(Job job, DateTime nowUtc) =>
        WaitingTimePolicy.Waits(job, FreeMinutes, RatePerHour, nowUtc).Select(w => WaitDto.From(w, RatePerHour, Currency)).ToList();

    /// <summary>
    /// Invoice lines for finished, chargeable waits with trustworthy evidence. A driver's tap far from the address
    /// is not suggested (the office can still add it by hand).
    /// </summary>
    public IReadOnlyList<SuggestedLineDto> SuggestedLines(Job job, DateTime nowUtc)
    {
        if (RatePerHour is not { } rate)
        {
            return [];
        }

        return WaitingTimePolicy.Waits(job, FreeMinutes, rate, nowUtc)
            .Where(w => w is { ChargedMinutes: > 0, Verified: true, Running: false })
            .Select(w => new SuggestedLineDto(Describe(job, w), w.ChargedMinutes / 60m, rate))
            .ToList();
    }

    public static string Duration(int minutes) =>
        minutes < 60 ? $"{minutes} min" : minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes / 60} h {minutes % 60} min";

    private string Describe(Job job, StopWait w)
    {
        var pickup = w.Stop == WaitingTimePolicy.Pickup;
        var place = pickup ? job.Origin : job.Destination;
        return $"Waiting time at {(pickup ? "loading" : "unloading")}{(string.IsNullOrWhiteSpace(place) ? "" : $", {place}")}: "
            + $"arrived {Clock(w.ArrivedAtUtc)}, released {Clock(w.ReleasedAtUtc!.Value)} ({Duration(w.WaitedMinutes)}, "
            + $"{Duration(w.FreeMinutes)} free), {Duration(w.ChargedMinutes)} charged";
    }

    private string Clock(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
        return Zone == TimeZoneInfo.Utc
            ? local.ToString("d MMM HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture)
            : local.ToString("d MMM HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    }
}
