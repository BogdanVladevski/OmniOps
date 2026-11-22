using OmniOps.Application.Dtos;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;

namespace OmniOps.Application.Services;

/// <summary>A company's waiting-time terms (free time, hourly rate) applied to a job.</summary>
public sealed record WaitingTerms(int FreeMinutes, decimal? RatePerHour, string Currency, TimeZoneInfo Zone)
{
    public static readonly WaitingTerms Default = new(120, null, "EUR", TimeZoneInfo.Utc);

    public static WaitingTerms From(BillingProfile? profile) =>
        profile is null ? Default : new(profile.FreeWaitingMinutes, profile.WaitingRatePerHour, profile.Currency, ZoneOf(profile.TimeZone));

    public static TimeZoneInfo ZoneOf(string? id)
    {
        try
        {
            return string.IsNullOrWhiteSpace(id) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public IReadOnlyList<WaitDto> Waits(Trip job, DateTime nowUtc) =>
        WaitingTimePolicy.Waits(job, FreeMinutes, RatePerHour, nowUtc).Select(w => WaitDto.From(w, RatePerHour, Currency)).ToList();

    /// <summary>
    /// Invoice lines for finished, chargeable waits with trustworthy evidence. A driver's tap far from the address
    /// is not suggested (the office can still add it by hand).
    /// </summary>
    public IReadOnlyList<SuggestedLineDto> SuggestedLines(Trip job, DateTime nowUtc)
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

    private string Describe(Trip job, StopWait w)
    {
        var place = w.Stop == WaitingTimePolicy.Pickup ? job.Origin : job.Destination;
        var where = w.Stop == WaitingTimePolicy.Pickup ? "loading" : "unloading";
        return $"Waiting time at {where}{(string.IsNullOrWhiteSpace(place) ? "" : $", {place}")}: "
            + $"arrived {Clock(w.ArrivedAtUtc)}, released {Clock(w.ReleasedAtUtc!.Value)} ({Duration(w.WaitedMinutes)}, "
            + $"{Duration(w.FreeMinutes)} free), {Duration(w.ChargedMinutes)} charged";
    }

    public static string Duration(int minutes) =>
        minutes < 60 ? $"{minutes} min" : minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes / 60} h {minutes % 60} min";

    private string Clock(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
        return Zone == TimeZoneInfo.Utc ? $"{local:d MMM HH:mm} UTC" : $"{local:d MMM HH:mm}";
    }
}
