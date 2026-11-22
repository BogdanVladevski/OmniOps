using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Dtos;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Exceptions;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

/// <summary>A driver sign-in only ever sees their own jobs; anything else looks like it does not exist.</summary>
internal static class JobAccess
{
    public static Trip Visible(Trip? job, Guid jobId, ITenantContext tenant) =>
        job is not null && (tenant.DriverId is not { } driverId || job.DriverId == driverId)
            ? job
            : throw new KeyNotFoundException($"Job {jobId} not found.");
}

/// <summary>Alert types broadcast for delivery-job milestones (the app maps these to labels).</summary>
public static class JobAlerts
{
    public const string Delivered = "JobDelivered";
    public const string ProblemReported = "JobProblemReported";
    public const string WaitingCharged = "JobWaitingCharged";
}

public class CreateJobCommandHandler : IRequestHandler<CreateJobCommand, JobDto>
{
    private readonly IJobRepository _jobs;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public CreateJobCommandHandler(IJobRepository jobs, ITenantContext tenant, IAuditService audit)
    {
        _jobs = jobs;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<JobDto> Handle(CreateJobCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _tenant.OrganizationId;
        var vehicle = await _jobs.GetVehicleAsync(request.VehicleId, organizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Vehicle {request.VehicleId} not found.");

        Driver? driver = null;
        if (request.DriverId is { } driverId)
        {
            driver = await _jobs.GetDriverAsync(driverId, organizationId, cancellationToken)
                ?? throw new KeyNotFoundException($"Driver {driverId} not found.");
        }

        var job = new Trip
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FleetId = vehicle.FleetId,
            VehicleId = vehicle.Id,
            DriverId = driver?.Id,
            Status = TripStatus.Planned,
            CreatedAtUtc = DateTime.UtcNow,
            CustomerName = request.CustomerName.Trim(),
            Reference = request.Reference?.Trim(),
            Origin = request.Origin?.Trim(),
            Destination = request.Destination.Trim(),
            OriginLatitude = request.OriginLatitude,
            OriginLongitude = request.OriginLongitude,
            DestinationLatitude = request.DestinationLatitude,
            DestinationLongitude = request.DestinationLongitude,
            PlannedDeliveryUtc = request.PlannedDeliveryUtc?.UtcDateTime,
            MinTempCelsius = request.MinTempCelsius,
            MaxTempCelsius = request.MaxTempCelsius,
            TrackingToken = TrackingToken.Create(),
            Vehicle = vehicle,
            Driver = driver,
        };

        await _jobs.AddAsync(job, cancellationToken);
        // Audit saves pending changes too, so the job and its audit record commit together.
        await _audit.LogAsync("Create", "Job", job.Id.ToString(), _tenant.UserId,
            $"{job.CustomerName} → {job.Destination}", cancellationToken);
        return JobDto.FromEntity(job);
    }
}

public class StartJobCommandHandler : IRequestHandler<StartJobCommand, JobDto>
{
    private readonly IJobRepository _jobs;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public StartJobCommandHandler(IJobRepository jobs, ITenantContext tenant, IAuditService audit)
    {
        _jobs = jobs;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<JobDto> Handle(StartJobCommand request, CancellationToken cancellationToken)
    {
        var job = JobAccess.Visible(
            await _jobs.GetAsync(request.JobId, _tenant.OrganizationId, cancellationToken), request.JobId, _tenant);

        switch (job.Status)
        {
            case TripStatus.InProgress:
                return JobDto.FromEntity(job);
            case TripStatus.Completed:
                throw new BusinessRuleException("This job is already delivered.");
            case TripStatus.Cancelled:
                throw new BusinessRuleException("This job was cancelled.");
        }

        job.Status = TripStatus.InProgress;
        job.StartedAtUtc = PhoneClock.Resolve(request.StartedAt, job.CreatedAtUtc, DateTime.UtcNow);
        await _audit.LogAsync("Start", "Job", job.Id.ToString(), _tenant.UserId, null, cancellationToken);
        return JobDto.FromEntity(job);
    }
}

public class RecordJobLocationsCommandHandler : IRequestHandler<RecordJobLocationsCommand, JobLocationsResultDto>
{
    /// <summary>Points the phone buffered just before delivery may arrive after it; accept them for this long.</summary>
    private static readonly TimeSpan LateArrivalAllowance = TimeSpan.FromMinutes(5);

    private readonly IJobRepository _jobs;
    private readonly ITelemetryRepository _telemetry;
    private readonly ITenantContext _tenant;
    private readonly IMediator _mediator;
    private readonly ILogger<RecordJobLocationsCommandHandler> _logger;

    public RecordJobLocationsCommandHandler(
        IJobRepository jobs,
        ITelemetryRepository telemetry,
        ITenantContext tenant,
        IMediator mediator,
        ILogger<RecordJobLocationsCommandHandler> logger)
    {
        _jobs = jobs;
        _telemetry = telemetry;
        _tenant = tenant;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<JobLocationsResultDto> Handle(RecordJobLocationsCommand request, CancellationToken cancellationToken)
    {
        var job = JobAccess.Visible(
            await _jobs.GetAsync(request.JobId, _tenant.OrganizationId, cancellationToken), request.JobId, _tenant);

        if (job.Status == TripStatus.Planned)
        {
            throw new BusinessRuleException("Start the job before sending locations.");
        }

        if (job.Status == TripStatus.Cancelled)
        {
            throw new BusinessRuleException("This job was cancelled.");
        }

        var points = request.Points.OrderBy(p => p.RecordedAtUtc).ToList();
        if (job.Status == TripStatus.Completed && job.CompletedAtUtc is { } deliveredAt)
        {
            points = points.Where(p => p.RecordedAtUtc.UtcDateTime <= deliveredAt + LateArrivalAllowance).ToList();
        }

        var existing = await _telemetry.GetExistingIdsAsync(points.Select(p => p.Id).ToList(), cancellationToken);
        var fresh = points.Where(p => !existing.Contains(p.Id)).DistinctBy(p => p.Id).ToList();

        // The job tells us exactly whose truck this is, so skip the global external-id lookup.
        var tenant = new VehicleTenant(job.OrganizationId, job.FleetId);
        foreach (var point in fresh)
        {
            // A TransientProcessingException here propagates (503): points already stored are skipped
            // on the phone's retry, the rest are processed then.
            await _mediator.Send(new ProcessTelemetryCommand(ToTelemetry(point, job.Vehicle.ExternalId), tenant), cancellationToken);
        }

        var changed = TrackDeliveryArrival(job, fresh);
        var latest = fresh.LastOrDefault();
        if (latest is not null && (job.LastPositionAtUtc is null || latest.RecordedAtUtc.UtcDateTime > job.LastPositionAtUtc))
        {
            job.LastLatitude = latest.Latitude;
            job.LastLongitude = latest.Longitude;
            job.LastSpeedKmh = latest.SpeedKmh;
            job.LastPositionAtUtc = latest.RecordedAtUtc.UtcDateTime;
            changed = true;
        }

        if (changed)
        {
            await _jobs.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Job {JobId}: stored {Accepted} location point(s), skipped {Skipped}",
            job.Id, fresh.Count, request.Points.Count - fresh.Count);

        return new JobLocationsResultDto(fresh.Count, request.Points.Count - fresh.Count);
    }

    /// <summary>
    /// The waiting clock at the delivery starts when GPS shows the truck at the address. A driver's tap near the
    /// address stands; GPS replaces a tap that was far away, and clears its own arrival if the truck drives off.
    /// </summary>
    private static bool TrackDeliveryArrival(Trip job, IReadOnlyList<JobLocationPoint> points)
    {
        if (points.Count == 0 || job.DestinationLatitude is not { } lat || job.DestinationLongitude is not { } lon)
        {
            return false;
        }

        var trustedTap = job.DeliveryArrivalSource == ArrivalSource.Driver
            && WaitingTimePolicy.Waits(job, 0, null, DateTime.UtcNow).Any(w => w.Stop == WaitingTimePolicy.Delivery && w.Verified);
        if (trustedTap)
        {
            return false;
        }

        // Readings from before the trip started (left over in a phone's queue) say nothing about this delivery.
        var since = job.StartedAtUtc ?? job.CreatedAtUtc;
        var relevant = points.Where(p => p.RecordedAtUtc.UtcDateTime >= since).ToList();
        if (relevant.Count == 0)
        {
            return false;
        }

        var current = job.DeliveryArrivalSource == ArrivalSource.Gps ? job.DeliveryArrivedAtUtc : null;
        var arrival = WaitingTimePolicy.TrackArrival(
            current, relevant.Select(p => (p.RecordedAtUtc.UtcDateTime, p.Latitude, p.Longitude)), lat, lon);
        if (arrival == current || (arrival is null && job.DeliveryArrivalSource == ArrivalSource.Driver))
        {
            return false;
        }

        var at = arrival is { } t ? points.FirstOrDefault(p => p.RecordedAtUtc.UtcDateTime == t) : null;
        job.DeliveryArrivedAtUtc = arrival;
        job.DeliveryArrivalSource = arrival is null ? null : ArrivalSource.Gps;
        job.DeliveryArrivalLatitude = at?.Latitude ?? (arrival is null ? null : job.DeliveryArrivalLatitude);
        job.DeliveryArrivalLongitude = at?.Longitude ?? (arrival is null ? null : job.DeliveryArrivalLongitude);
        if (arrival is null)
        {
            job.DeliveryWaitAlertedAtUtc = null;
        }

        return true;
    }

    private static VehicleTelemetry ToTelemetry(JobLocationPoint point, string vehicleExternalId) => new()
    {
        Id = point.Id,
        VehicleId = vehicleExternalId,
        Latitude = point.Latitude,
        Longitude = point.Longitude,
        Speed = Math.Max(0, point.SpeedKmh ?? 0),
        Heading = point.Heading,
        // A phone measures neither fuel nor cargo temperature; leave them empty rather than 0.
        FuelLevel = null,
        EngineTemperature = null,
        SensorReadingsJson = JsonSerializer.Serialize(new { source = "phone", accuracyMeters = point.AccuracyMeters }),
        Timestamp = point.RecordedAtUtc.UtcDateTime,
    };
}

public class AddJobDocumentCommandHandler : IRequestHandler<AddJobDocumentCommand, JobDocumentDto>
{
    private readonly IJobRepository _jobs;
    private readonly IFileStore _files;
    private readonly ITenantContext _tenant;
    private readonly ITelemetryBroadcastService _broadcast;
    private readonly ILogger<AddJobDocumentCommandHandler> _logger;

    public AddJobDocumentCommandHandler(
        IJobRepository jobs,
        IFileStore files,
        ITenantContext tenant,
        ITelemetryBroadcastService broadcast,
        ILogger<AddJobDocumentCommandHandler> logger)
    {
        _jobs = jobs;
        _files = files;
        _tenant = tenant;
        _broadcast = broadcast;
        _logger = logger;
    }

    public async Task<JobDocumentDto> Handle(AddJobDocumentCommand request, CancellationToken cancellationToken)
    {
        var job = JobAccess.Visible(
            await _jobs.GetAsync(request.JobId, _tenant.OrganizationId, cancellationToken), request.JobId, _tenant);

        // A resend of an upload the server already has: same answer, nothing stored twice.
        if (request.ClientId is { } clientId && job.Documents.FirstOrDefault(d => d.ClientId == clientId) is { } existing)
        {
            return JobDocumentDto.FromEntity(existing);
        }

        EnsureAllowed(job, request);

        var now = DateTime.UtcNow;
        var extension = AddJobDocumentCommand.AllowedTypes[request.ContentType];
        var document = new TripDocument
        {
            Id = Guid.NewGuid(),
            OrganizationId = job.OrganizationId,
            TripId = job.Id,
            Kind = request.Kind,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            CapturedAtUtc = PhoneClock.Resolve(request.CapturedAt, job.CreatedAtUtc, now),
            ReceivedAtUtc = now,
            ClientId = request.ClientId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            TemperatureCelsius = request.Kind == TripDocumentKind.TemperaturePhoto ? request.TemperatureCelsius : null,
            Note = request.Kind is TripDocumentKind.Signature or TripDocumentKind.Exception && !string.IsNullOrWhiteSpace(request.Note)
                ? request.Note.Trim()
                : null,
        };
        document.StorageKey = $"{job.OrganizationId:N}/{job.Id:N}/{document.Id:N}.{extension}";

        // File first: a failed database write leaves an orphan file, never a record pointing at nothing.
        await _files.SaveAsync(document.StorageKey, request.Content, cancellationToken);
        await _jobs.AddDocumentAsync(document, cancellationToken);
        await _jobs.SaveChangesAsync(cancellationToken);

        // Saved — now an out-of-range reading reaches the office straight away.
        if (document.TemperatureCelsius is { } celsius && JobTemperaturePolicy.IsOutOfRange(job, celsius))
        {
            await AlertOutOfRangeAsync(job, celsius, cancellationToken);
        }

        if (document.Kind == TripDocumentKind.Exception)
        {
            await AlertProblemAsync(job, document.Note, cancellationToken);
        }

        return JobDocumentDto.FromEntity(document);
    }

    private static void EnsureAllowed(Trip job, AddJobDocumentCommand request)
    {
        var count = job.Documents.Count(d => d.Kind == request.Kind);
        switch (request.Kind)
        {
            case TripDocumentKind.PickupDocument:
                // Taken at loading, before or just after the trip starts.
                if (job.Status is not (TripStatus.Planned or TripStatus.InProgress))
                {
                    throw new BusinessRuleException("This job is closed; documents can no longer be added.");
                }

                break;
            case TripDocumentKind.TemperaturePhoto:
                if (job.Status is not (TripStatus.Planned or TripStatus.InProgress))
                {
                    throw new BusinessRuleException("This job is closed; temperature readings can no longer be added.");
                }

                if (count >= AddJobDocumentCommand.MaxTemperatureReadings)
                {
                    throw new BusinessRuleException($"A job can have at most {AddJobDocumentCommand.MaxTemperatureReadings} temperature readings.");
                }

                return;
            case TripDocumentKind.Exception:
                // Damage at loading is as important as damage at delivery.
                if (job.Status is not (TripStatus.Planned or TripStatus.InProgress))
                {
                    throw new BusinessRuleException("This job is closed; problems can no longer be reported on it.");
                }

                break;
            case TripDocumentKind.Signature:
                if (job.Status != TripStatus.InProgress)
                {
                    throw new BusinessRuleException(job.Status == TripStatus.Planned
                        ? "Start the job before taking the receiver's signature."
                        : "This job is closed; a signature can no longer be added.");
                }

                if (count >= AddJobDocumentCommand.MaxSignatures)
                {
                    throw new BusinessRuleException($"A job can have at most {AddJobDocumentCommand.MaxSignatures} signatures.");
                }

                return;
            default:
                if (job.Status != TripStatus.InProgress)
                {
                    throw new BusinessRuleException(job.Status == TripStatus.Planned
                        ? "Start the job before adding delivery photos."
                        : "This job is closed; photos can no longer be added.");
                }

                break;
        }

        if (count >= AddJobDocumentCommand.MaxPhotosPerKind)
        {
            throw new BusinessRuleException($"A job can have at most {AddJobDocumentCommand.MaxPhotosPerKind} photos of this kind.");
        }
    }

    private async Task AlertProblemAsync(Trip job, string? note, CancellationToken cancellationToken)
    {
        try
        {
            await _broadcast.BroadcastAlertAsync(
                job.OrganizationId,
                job.Vehicle.ExternalId,
                JobAlerts.ProblemReported,
                $"Problem reported — {job.Vehicle.ExternalId}",
                $"{job.CustomerName ?? job.Destination}: {note}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Problem report broadcast failed for job {JobId}", job.Id);
        }
    }

    private async Task AlertOutOfRangeAsync(Trip job, double celsius, CancellationToken cancellationToken)
    {
        try
        {
            await _broadcast.BroadcastAlertAsync(
                job.OrganizationId,
                job.Vehicle.ExternalId,
                CargoTemperatureAlerts.Excursion,
                $"Load outside its temperature range — {job.Vehicle.ExternalId}",
                $"{job.CustomerName ?? job.Destination}: driver logged {celsius:0.#} °C, " +
                $"safe range {job.MinTempCelsius:0.#}–{job.MaxTempCelsius:0.#} °C.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Never lose the reading because the live alert failed; it is in the log either way.
            _logger.LogWarning(ex, "Temperature alert broadcast failed for job {JobId}", job.Id);
        }
    }
}

public class DeliverJobCommandHandler : IRequestHandler<DeliverJobCommand, JobDto>
{
    private readonly IJobRepository _jobs;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;
    private readonly ITelemetryBroadcastService _broadcast;
    private readonly ILogger<DeliverJobCommandHandler> _logger;

    public DeliverJobCommandHandler(
        IJobRepository jobs,
        ITenantContext tenant,
        IAuditService audit,
        ITelemetryBroadcastService broadcast,
        ILogger<DeliverJobCommandHandler> logger)
    {
        _jobs = jobs;
        _tenant = tenant;
        _audit = audit;
        _broadcast = broadcast;
        _logger = logger;
    }

    public async Task<JobDto> Handle(DeliverJobCommand request, CancellationToken cancellationToken)
    {
        var job = JobAccess.Visible(
            await _jobs.GetAsync(request.JobId, _tenant.OrganizationId, cancellationToken), request.JobId, _tenant);

        switch (job.Status)
        {
            case TripStatus.Completed:
                return JobDto.FromEntity(job);
            case TripStatus.Planned:
                throw new BusinessRuleException("Start the job before marking it delivered.");
            case TripStatus.Cancelled:
                throw new BusinessRuleException("This job was cancelled.");
        }

        if (!job.Documents.Any(d => d.Kind is TripDocumentKind.DeliveryPhoto or TripDocumentKind.Signature))
        {
            throw new BusinessRuleException("Add a delivery photo or the receiver's signature before confirming delivery.");
        }

        var deliveredAt = PhoneClock.Resolve(request.DeliveredAt, job.StartedAtUtc ?? job.CreatedAtUtc, DateTime.UtcNow);
        if (JobTemperaturePolicy.IsRefrigerated(job) && !JobTemperaturePolicy.HasRecentReading(job, deliveredAt))
        {
            throw new BusinessRuleException("Log the load temperature (photo of the display) before confirming delivery.");
        }

        job.Status = TripStatus.Completed;
        job.CompletedAtUtc = deliveredAt;
        job.RecipientName = request.RecipientName.Trim();
        job.DeliveryNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        job.DeliveredLatitude = request.Latitude;
        job.DeliveredLongitude = request.Longitude;

        // Saves the job and the audit record together.
        await _audit.LogAsync("Deliver", "Job", job.Id.ToString(), _tenant.UserId,
            $"Received by {job.RecipientName}", cancellationToken);

        // Committed — now tell the office.
        try
        {
            var who = job.CustomerName ?? job.Destination ?? "the customer";
            await _broadcast.BroadcastAlertAsync(
                job.OrganizationId,
                job.Vehicle.ExternalId,
                JobAlerts.Delivered,
                $"Delivered to {who}",
                $"{job.Vehicle.ExternalId} delivered{(job.Reference is null ? "" : $" order {job.Reference}")} — received by {job.RecipientName}.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Delivery broadcast failed for job {JobId}", job.Id);
        }

        return JobDto.FromEntity(job);
    }
}

public class MarkArrivedCommandHandler : IRequestHandler<MarkArrivedCommand, JobDto>
{
    private readonly IJobRepository _jobs;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public MarkArrivedCommandHandler(IJobRepository jobs, ITenantContext tenant, IAuditService audit)
    {
        _jobs = jobs;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<JobDto> Handle(MarkArrivedCommand request, CancellationToken cancellationToken)
    {
        var job = JobAccess.Visible(
            await _jobs.GetAsync(request.JobId, _tenant.OrganizationId, cancellationToken), request.JobId, _tenant);
        var now = DateTime.UtcNow;

        if (request.Stop == WaitingTimePolicy.Pickup)
        {
            // Waiting at pickup ends when the trip starts, so it can only begin before that.
            if (job.Status != TripStatus.Planned)
            {
                if (job.PickupArrivedAtUtc is not null || job.Status == TripStatus.InProgress)
                {
                    return JobDto.FromEntity(job);
                }

                throw new BusinessRuleException("This job is closed.");
            }

            var at = PhoneClock.Resolve(request.ArrivedAt, job.CreatedAtUtc, now);
            if (job.PickupArrivedAtUtc is { } existing && existing <= at)
            {
                return JobDto.FromEntity(job);
            }

            job.PickupArrivedAtUtc = at;
            job.PickupArrivalSource = ArrivalSource.Driver;
            job.PickupArrivalLatitude = request.Latitude;
            job.PickupArrivalLongitude = request.Longitude;
        }
        else
        {
            if (job.Status == TripStatus.Completed && job.DeliveryArrivedAtUtc is not null)
            {
                return JobDto.FromEntity(job);
            }

            if (job.Status != TripStatus.InProgress)
            {
                throw new BusinessRuleException(job.Status == TripStatus.Planned
                    ? "Start the trip first."
                    : "This job is closed.");
            }

            var at = PhoneClock.Resolve(request.ArrivedAt, job.StartedAtUtc ?? job.CreatedAtUtc, now);
            // GPS may already have it; the earlier, better-evidenced arrival stays.
            if (job.DeliveryArrivedAtUtc is { } existing && existing <= at)
            {
                return JobDto.FromEntity(job);
            }

            job.DeliveryArrivedAtUtc = at;
            job.DeliveryArrivalSource = ArrivalSource.Driver;
            job.DeliveryArrivalLatitude = request.Latitude;
            job.DeliveryArrivalLongitude = request.Longitude;
        }

        await _audit.LogAsync("Arrived", "Job", job.Id.ToString(), _tenant.UserId, request.Stop, cancellationToken);
        return JobDto.FromEntity(job);
    }
}

public class ProcessWaitingAlertsCommandHandler : IRequestHandler<ProcessWaitingAlertsCommand, int>
{
    private readonly IJobRepository _jobs;
    private readonly IInvoiceRepository _billing;
    private readonly ITelemetryBroadcastService _broadcast;
    private readonly TimeProvider _clock;
    private readonly ILogger<ProcessWaitingAlertsCommandHandler> _logger;

    public ProcessWaitingAlertsCommandHandler(
        IJobRepository jobs, IInvoiceRepository billing, ITelemetryBroadcastService broadcast, TimeProvider clock,
        ILogger<ProcessWaitingAlertsCommandHandler> logger)
    {
        _jobs = jobs;
        _billing = billing;
        _broadcast = broadcast;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> Handle(ProcessWaitingAlertsCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var sent = 0;
        var terms = new Dictionary<Guid, Services.WaitingTerms>();
        foreach (var job in await _jobs.ListWaitingAsync(cancellationToken))
        {
            if (!terms.TryGetValue(job.OrganizationId, out var t))
            {
                t = Services.WaitingTerms.From(await _billing.GetProfileAsync(job.OrganizationId, cancellationToken));
                terms[job.OrganizationId] = t;
            }

            foreach (var wait in WaitingTimePolicy.Waits(job, t.FreeMinutes, t.RatePerHour, now))
            {
                var pickup = wait.Stop == WaitingTimePolicy.Pickup;
                var alerted = pickup ? job.PickupWaitAlertedAtUtc : job.DeliveryWaitAlertedAtUtc;
                if (!wait.Running || alerted is not null || now < wait.ChargedFromUtc)
                {
                    continue;
                }

                if (pickup) job.PickupWaitAlertedAtUtc = now;
                else job.DeliveryWaitAlertedAtUtc = now;

                // Saved first: the alert must not repeat if the broadcast or a later job fails.
                await _jobs.SaveChangesAsync(cancellationToken);
                sent++;
                try
                {
                    var where = pickup ? job.Origin ?? "pickup" : job.Destination ?? "delivery";
                    await _broadcast.BroadcastAlertAsync(
                        job.OrganizationId,
                        job.Vehicle.ExternalId,
                        JobAlerts.WaitingCharged,
                        $"Free waiting time is over — {job.Vehicle.ExternalId} at {where}",
                        $"{job.CustomerName ?? where}: waiting since {wait.ArrivedAtUtc:HH:mm} UTC ({Services.WaitingTerms.Duration(wait.WaitedMinutes)})."
                        + (t.RatePerHour is { } rate ? $" Waiting is now charged at {rate:0.##} {t.Currency}/h." : string.Empty),
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Waiting alert broadcast failed for job {JobId}", job.Id);
                }
            }
        }

        return sent;
    }
}
