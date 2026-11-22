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

        var latest = fresh.LastOrDefault();
        if (latest is not null && (job.LastPositionAtUtc is null || latest.RecordedAtUtc.UtcDateTime > job.LastPositionAtUtc))
        {
            job.LastLatitude = latest.Latitude;
            job.LastLongitude = latest.Longitude;
            job.LastSpeedKmh = latest.SpeedKmh;
            job.LastPositionAtUtc = latest.RecordedAtUtc.UtcDateTime;
            await _jobs.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Job {JobId}: stored {Accepted} location point(s), skipped {Skipped}",
            job.Id, fresh.Count, request.Points.Count - fresh.Count);

        return new JobLocationsResultDto(fresh.Count, request.Points.Count - fresh.Count);
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
