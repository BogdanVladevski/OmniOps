using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Domain.Common;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Jobs;

/// <summary>What the receiver's tracking page may show: nothing about the driver, the truck or other jobs.</summary>
public sealed record PublicTrackingDto(
    string CompanyName,
    string? Reference,
    string? CustomerName,
    string Status,
    string? Origin,
    string? Destination,
    double? DestinationLatitude,
    double? DestinationLongitude,
    DateTime? PlannedDeliveryUtc,
    double? Latitude,
    double? Longitude,
    DateTime? PositionAtUtc,
    double? RemainingKm,
    DateTime? EtaUtc,
    DateTime? DeliveredAtUtc,
    string? RecipientName,
    DateTime? ArrivedAtUtc,
    DateTime? WaitingChargedFromUtc);

/// <summary>Anonymous: the token is the credential. Null for unknown, cancelled or expired links.</summary>
public sealed record GetPublicTrackingQuery(string Token) : IQuery<PublicTrackingDto?>;

public sealed class GetPublicTrackingHandler(IAppDbContext db, TimeProvider clock) : IQueryHandler<GetPublicTrackingQuery, PublicTrackingDto?>
{
    /// <summary>How long a delivered job's link keeps working.</summary>
    public static readonly TimeSpan LinkLifetimeAfterDelivery = TimeSpan.FromDays(7);

    public async ValueTask<PublicTrackingDto?> Handle(GetPublicTrackingQuery query, CancellationToken cancellationToken)
    {
        if (!PublicToken.LooksValid(query.Token))
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var job = await db.Jobs.AcrossCompanies().FirstOrDefaultAsync(j => j.TrackingToken == query.Token, cancellationToken);
        if (job is null || job.Status == JobStatus.Cancelled || job.DeliveredAtUtc < now - LinkLifetimeAfterDelivery)
        {
            return null;
        }

        var company = await db.Organizations.Where(o => o.Id == job.OrganizationId).Select(o => o.Name).FirstAsync(cancellationToken);

        // Live position only while the truck is on its way to this customer, never before or after.
        var onTheWay = job.Status == JobStatus.InProgress;
        double? lat = onTheWay ? job.LastLatitude : null, lon = onTheWay ? job.LastLongitude : null;
        DateTime? at = onTheWay ? job.LastPositionAtUtc : null;
        (double? km, DateTime? eta) estimate = (null, null);
        if (lat is { } la && lon is { } lo && at is { } positionAt && job.DestinationLatitude is { } dLat && job.DestinationLongitude is { } dLon)
        {
            estimate = DeliveryEta.Estimate(la, lo, dLat, dLon, positionAt);
        }

        // The receiver sees when the truck arrived and, if the carrier charges for waiting, from when it is charged.
        DateTime? chargedFrom = null;
        if (job.DeliveryArrivedAtUtc is not null)
        {
            var terms = await WaitingTerms.ForAsync(db, job.OrganizationId, cancellationToken);
            var wait = WaitingTimePolicy.Waits(job, terms.FreeMinutes, terms.RatePerHour, now)
                .FirstOrDefault(w => w.Stop == WaitingTimePolicy.Delivery && w.Verified);
            chargedFrom = terms.RatePerHour is not null ? wait?.ChargedFromUtc : null;
        }

        return new PublicTrackingDto(
            company, job.Reference, job.CustomerName,
            job.Status switch { JobStatus.InProgress => "OnTheWay", JobStatus.Delivered => "Delivered", _ => "Planned" },
            job.Origin, job.Destination, job.DestinationLatitude, job.DestinationLongitude, job.PlannedDeliveryUtc,
            lat, lon, at, estimate.km, estimate.eta, job.DeliveredAtUtc,
            job.Status == JobStatus.Delivered ? job.RecipientName : null,
            job.DeliveryArrivedAtUtc, chargedFrom);
    }
}
