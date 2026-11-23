using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Domain.Common;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Jobs;

public sealed record LocationPoint(
    Guid Id,
    double Latitude,
    double Longitude,
    double? SpeedKmh,
    double? Heading,
    double? AccuracyMeters,
    // Offset-aware: phones may send local time with an offset ("+02:00").
    DateTimeOffset RecordedAtUtc);

public sealed record RecordLocationsResultDto(int Accepted, int Skipped);

/// <summary>
/// GPS readings from the driver's phone. Processed synchronously so the phone keeps (and resends) anything not
/// acknowledged; readings already stored are skipped, so resending is always safe.
/// </summary>
[RequiresPolicy(Policies.JobWork)]
public sealed record RecordLocationsCommand(Guid JobId, IReadOnlyList<LocationPoint> Points) : ICommand<RecordLocationsResultDto>
{
    public const int MaxPointsPerBatch = 500;

}

public sealed class RecordLocationsValidator : AbstractValidator<RecordLocationsCommand>
{
    public RecordLocationsValidator()
    {
        RuleFor(x => x.Points).NotNull().Must(p => p.Count is > 0 and <= RecordLocationsCommand.MaxPointsPerBatch)
            .WithMessage($"Send between 1 and {RecordLocationsCommand.MaxPointsPerBatch} points per request.");
        RuleForEach(x => x.Points).ChildRules(point =>
        {
            point.RuleFor(p => p.Id).NotEmpty();
            point.RuleFor(p => p.Latitude).InclusiveBetween(-90, 90);
            point.RuleFor(p => p.Longitude).InclusiveBetween(-180, 180);
            point.RuleFor(p => p.SpeedKmh).InclusiveBetween(0, 300).When(p => p.SpeedKmh.HasValue);
            point.RuleFor(p => p.Heading).InclusiveBetween(0, 360).When(p => p.Heading.HasValue);
            point.RuleFor(p => p.AccuracyMeters).GreaterThanOrEqualTo(0).When(p => p.AccuracyMeters.HasValue);
            point.RuleFor(p => p.RecordedAtUtc)
                .Must(t => t > DateTimeOffset.UtcNow.AddDays(-7) && t < DateTimeOffset.UtcNow.AddMinutes(5))
                .WithMessage("Point time must be within the last 7 days and not in the future.");
        });
    }
}

public sealed class RecordLocationsHandler(IAppDbContext db, ITenantContext tenant) : ICommandHandler<RecordLocationsCommand, RecordLocationsResultDto>
{
    /// <summary>Readings the phone buffered just before delivery may arrive after it; accepted for this long.</summary>
    private static readonly TimeSpan LateArrivalAllowance = TimeSpan.FromMinutes(5);

    public async ValueTask<RecordLocationsResultDto> Handle(RecordLocationsCommand command, CancellationToken cancellationToken)
    {
        var job = await db.VisibleJobAsync(tenant, command.JobId, cancellationToken);
        if (job.Status == JobStatus.Planned)
        {
            throw new BusinessRuleException("Start the job before sending locations.");
        }

        if (job.Status == JobStatus.Cancelled)
        {
            throw new BusinessRuleException("This job was cancelled.");
        }

        var points = command.Points.OrderBy(p => p.RecordedAtUtc).ToList();
        if (job is { Status: JobStatus.Delivered, DeliveredAtUtc: { } deliveredAt })
        {
            points = points.Where(p => p.RecordedAtUtc.UtcDateTime <= deliveredAt + LateArrivalAllowance).ToList();
        }

        var ids = points.Select(p => p.Id).ToList();
        var stored = await db.JobLocations.Where(l => ids.Contains(l.Id)).Select(l => l.Id).ToListAsync(cancellationToken);
        var fresh = points.Where(p => !stored.Contains(p.Id)).DistinctBy(p => p.Id).ToList();

        db.JobLocations.AddRange(fresh.Select(p => new JobLocation
        {
            Id = p.Id,
            JobId = job.Id,
            OrganizationId = job.OrganizationId,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            SpeedKmh = p.SpeedKmh,
            Heading = p.Heading,
            AccuracyMeters = p.AccuracyMeters,
            RecordedAtUtc = p.RecordedAtUtc.UtcDateTime,
        }));

        TrackDeliveryArrival(job, fresh);
        if (fresh.LastOrDefault() is { } latest && (job.LastPositionAtUtc is null || latest.RecordedAtUtc.UtcDateTime > job.LastPositionAtUtc))
        {
            job.LastLatitude = latest.Latitude;
            job.LastLongitude = latest.Longitude;
            job.LastSpeedKmh = latest.SpeedKmh;
            job.LastPositionAtUtc = latest.RecordedAtUtc.UtcDateTime;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new RecordLocationsResultDto(fresh.Count, command.Points.Count - fresh.Count);
    }

    /// <summary>
    /// The waiting clock at the delivery starts when GPS shows the truck at the address. A driver's tap near the
    /// address stands; GPS replaces a tap that was far away, and clears its own arrival if the truck drives off.
    /// </summary>
    private static void TrackDeliveryArrival(Job job, IReadOnlyList<LocationPoint> points)
    {
        if (job.DestinationLatitude is not { } lat || job.DestinationLongitude is not { } lon)
        {
            return;
        }

        var trustedTap = job.DeliveryArrivalSource == ArrivalSource.Driver
            && WaitingTimePolicy.Waits(job, 0, null, DateTime.UtcNow).Any(w => w.Stop == WaitingTimePolicy.Delivery && w.Verified);
        if (trustedTap)
        {
            return;
        }

        // Readings from before the job started (left over in a phone's queue) say nothing about this delivery.
        var since = job.StartedAtUtc ?? job.CreatedAtUtc;
        var relevant = points.Where(p => p.RecordedAtUtc.UtcDateTime >= since).ToList();
        if (relevant.Count == 0)
        {
            return;
        }

        var current = job.DeliveryArrivalSource == ArrivalSource.Gps ? job.DeliveryArrivedAtUtc : null;
        var arrival = WaitingTimePolicy.TrackArrival(current, relevant.Select(p => (p.RecordedAtUtc.UtcDateTime, p.Latitude, p.Longitude)), lat, lon);
        if (arrival == current || (arrival is null && job.DeliveryArrivalSource == ArrivalSource.Driver))
        {
            return;
        }

        var at = arrival is { } t ? relevant.FirstOrDefault(p => p.RecordedAtUtc.UtcDateTime == t) : null;
        job.DeliveryArrivedAtUtc = arrival;
        job.DeliveryArrivalSource = arrival is null ? null : ArrivalSource.Gps;
        job.DeliveryArrivalLatitude = at?.Latitude ?? (arrival is null ? null : job.DeliveryArrivalLatitude);
        job.DeliveryArrivalLongitude = at?.Longitude ?? (arrival is null ? null : job.DeliveryArrivalLongitude);
        if (arrival is null)
        {
            job.DeliveryWaitAlertedAtUtc = null;
        }
    }
}
