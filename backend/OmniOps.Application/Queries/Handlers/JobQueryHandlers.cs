using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Core.Domain;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetJobsQueryHandler : IRequestHandler<GetJobsQuery, IReadOnlyList<JobDto>>
{
    private const int MaxJobs = 200;
    private readonly IJobRepository _jobs;
    private readonly ITenantContext _tenant;

    public GetJobsQueryHandler(IJobRepository jobs, ITenantContext tenant)
    {
        _jobs = jobs;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<JobDto>> Handle(GetJobsQuery request, CancellationToken cancellationToken)
    {
        var (statuses, completedSince) = request.Scope switch
        {
            JobScope.Active => (new[] { TripStatus.Planned, TripStatus.InProgress, TripStatus.Completed },
                (DateTime?)DateTime.UtcNow.AddHours(-24)),
            JobScope.Delivered => (new[] { TripStatus.Completed }, null),
            _ => (Enum.GetValues<TripStatus>(), null),
        };

        // A driver always gets their own jobs, whatever driver id the request names.
        var driverId = _tenant.DriverId ?? request.DriverId;
        var jobs = await _jobs.ListAsync(
            _tenant.OrganizationId, statuses, driverId, completedSince, MaxJobs, cancellationToken);
        return jobs.Select(JobDto.FromEntity).ToList();
    }
}

public class GetJobQueryHandler : IRequestHandler<GetJobQuery, JobDetailDto>
{
    private readonly IJobRepository _jobs;
    private readonly IInvoiceRepository _billing;
    private readonly ITenantContext _tenant;

    public GetJobQueryHandler(IJobRepository jobs, IInvoiceRepository billing, ITenantContext tenant)
    {
        _jobs = jobs;
        _billing = billing;
        _tenant = tenant;
    }

    public async Task<JobDetailDto> Handle(GetJobQuery request, CancellationToken cancellationToken)
    {
        var job = await _jobs.GetAsync(request.JobId, _tenant.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Job {request.JobId} not found.");
        var terms = Services.WaitingTerms.From(await _billing.GetProfileAsync(_tenant.OrganizationId, cancellationToken));
        return new JobDetailDto(
            JobDto.FromEntity(job),
            job.Documents.OrderBy(d => d.CapturedAtUtc).Select(JobDocumentDto.FromEntity).ToList(),
            terms.Waits(job, DateTime.UtcNow));
    }
}

public class GetJobDocumentQueryHandler : IRequestHandler<GetJobDocumentQuery, JobDocumentContent>
{
    private readonly IJobRepository _jobs;
    private readonly IFileStore _files;
    private readonly ITenantContext _tenant;

    public GetJobDocumentQueryHandler(IJobRepository jobs, IFileStore files, ITenantContext tenant)
    {
        _jobs = jobs;
        _files = files;
        _tenant = tenant;
    }

    public async Task<JobDocumentContent> Handle(GetJobDocumentQuery request, CancellationToken cancellationToken)
    {
        var job = await _jobs.GetAsync(request.JobId, _tenant.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Job {request.JobId} not found.");
        var document = job.Documents.FirstOrDefault(d => d.Id == request.DocumentId)
            ?? throw new KeyNotFoundException($"Document {request.DocumentId} not found.");
        var stream = await _files.OpenReadAsync(document.StorageKey, cancellationToken)
            ?? throw new KeyNotFoundException($"Document {request.DocumentId} file is missing.");
        return new JobDocumentContent(stream, document.ContentType);
    }
}

public class GetJobAssignablesQueryHandler : IRequestHandler<GetJobAssignablesQuery, JobAssignablesDto>
{
    private readonly IJobRepository _jobs;
    private readonly ITenantContext _tenant;

    public GetJobAssignablesQueryHandler(IJobRepository jobs, ITenantContext tenant)
    {
        _jobs = jobs;
        _tenant = tenant;
    }

    public async Task<JobAssignablesDto> Handle(GetJobAssignablesQuery request, CancellationToken cancellationToken)
    {
        var vehicles = await _jobs.ListVehiclesAsync(_tenant.OrganizationId, cancellationToken);
        var drivers = await _jobs.ListDriversAsync(_tenant.OrganizationId, cancellationToken);
        return new JobAssignablesDto(
            vehicles.Select(v => new AssignableOptionDto(v.Id, v.ExternalId, v.Registration)).ToList(),
            drivers.Select(d => new AssignableOptionDto(d.Id, d.FullName, null)).ToList());
    }
}

public class GetPublicTrackingQueryHandler : IRequestHandler<GetPublicTrackingQuery, PublicTrackingDto?>
{
    /// <summary>How long a delivered job's link keeps working.</summary>
    public static readonly TimeSpan LinkLifetimeAfterDelivery = TimeSpan.FromDays(7);

    private readonly IJobRepository _jobs;
    private readonly ITenantRepository _tenants;
    private readonly IInvoiceRepository _billing;

    public GetPublicTrackingQueryHandler(IJobRepository jobs, ITenantRepository tenants, IInvoiceRepository billing)
    {
        _jobs = jobs;
        _tenants = tenants;
        _billing = billing;
    }

    public async Task<PublicTrackingDto?> Handle(GetPublicTrackingQuery request, CancellationToken cancellationToken)
    {
        if (!TrackingToken.LooksValid(request.Token))
        {
            return null;
        }

        var job = await _jobs.GetByTrackingTokenAsync(request.Token, cancellationToken);
        if (job is null || job.Status == TripStatus.Cancelled)
        {
            return null;
        }

        if (job.Status == TripStatus.Completed && job.CompletedAtUtc < DateTime.UtcNow - LinkLifetimeAfterDelivery)
        {
            return null;
        }

        var organization = await _tenants.GetOrganizationAsync(job.OrganizationId, cancellationToken);

        // The receiver sees when the truck arrived, and — if the carrier charges for waiting — from when it is charged.
        DateTime? arrivedAt = job.DeliveryArrivedAtUtc;
        DateTime? chargedFrom = null;
        if (arrivedAt is not null)
        {
            var terms = Services.WaitingTerms.From(await _billing.GetProfileAsync(job.OrganizationId, cancellationToken));
            var wait = WaitingTimePolicy.Waits(job, terms.FreeMinutes, terms.RatePerHour, DateTime.UtcNow)
                .FirstOrDefault(w => w.Stop == WaitingTimePolicy.Delivery && w.Verified);
            chargedFrom = terms.RatePerHour is not null && wait is not null ? wait.ChargedFromUtc : null;
        }

        // Live position only while the truck is on its way to this customer — never before or after.
        var onTheWay = job.Status == TripStatus.InProgress;
        double? lat = onTheWay ? job.LastLatitude : null;
        double? lon = onTheWay ? job.LastLongitude : null;
        DateTime? at = onTheWay ? job.LastPositionAtUtc : null;

        double? remainingKm = null;
        DateTime? eta = null;
        if (lat is { } la && lon is { } lo && at is { } positionAt
            && job.DestinationLatitude is { } dLat && job.DestinationLongitude is { } dLon)
        {
            var estimate = DeliveryEta.Estimate(la, lo, dLat, dLon, positionAt);
            remainingKm = estimate.RoadDistanceKm;
            eta = estimate.EtaUtc;
        }

        return new PublicTrackingDto(
            organization?.Name ?? "Your carrier",
            job.Reference,
            job.CustomerName,
            job.Status switch
            {
                TripStatus.InProgress => "OnTheWay",
                TripStatus.Completed => "Delivered",
                _ => "Planned",
            },
            job.Origin,
            job.Destination,
            job.DestinationLatitude,
            job.DestinationLongitude,
            job.PlannedDeliveryUtc,
            lat,
            lon,
            at,
            remainingKm,
            eta,
            job.CompletedAtUtc,
            job.Status == TripStatus.Completed ? job.RecipientName : null,
            arrivedAt,
            chargedFrom);
    }
}
