using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Domain.Common;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Jobs;

// What a driver does on a job, from their phone. Phones work offline and resend, so every action is safe to repeat,
// and times are taken from the phone (when it happened), kept between job creation and now.

/// <summary>Start the job. <paramref name="StartedAt"/> is when Start was tapped (the phone may have been offline).</summary>
[RequiresPolicy(Policies.JobWork)]
public sealed record StartJobCommand(Guid JobId, DateTimeOffset? StartedAt = null) : ICommand<JobDto>;

public sealed class StartJobHandler(IAppDbContext db, ITenantContext tenant, IAuditLog audit, IRealtimeNotifier notifier, TimeProvider clock)
    : ICommandHandler<StartJobCommand, JobDto>
{
    public async ValueTask<JobDto> Handle(StartJobCommand command, CancellationToken cancellationToken)
    {
        var job = await db.VisibleJobAsync(tenant, command.JobId, cancellationToken);
        switch (job.Status)
        {
            case JobStatus.InProgress:
                return JobDto.From(job);
            case JobStatus.Delivered:
                throw new BusinessRuleException("This job is already delivered.");
            case JobStatus.Cancelled:
                throw new BusinessRuleException("This job was cancelled.");
        }

        job.Status = JobStatus.InProgress;
        job.StartedAtUtc = PhoneClock.Resolve(command.StartedAt, job.CreatedAtUtc, clock.GetUtcNow().UtcDateTime);
        audit.Record("Start", "Job", job.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        await notifier.JobChangedAsync(job.OrganizationId, job.Id, cancellationToken);
        return JobDto.From(job);
    }
}

/// <summary>
/// "I've arrived" at the pickup (before starting) or the delivery. Starts the waiting clock with the phone's position as
/// evidence. GPS also detects delivery arrivals by itself; the earlier record wins.
/// </summary>
[RequiresPolicy(Policies.JobWork)]
public sealed record MarkArrivedCommand(Guid JobId, string Stop, DateTimeOffset? ArrivedAt, double? Latitude, double? Longitude)
    : ICommand<JobDto>;

public sealed class MarkArrivedValidator : AbstractValidator<MarkArrivedCommand>
{
    public MarkArrivedValidator()
    {
        RuleFor(x => x.Stop).Must(s => s is WaitingTimePolicy.Pickup or WaitingTimePolicy.Delivery).WithMessage("Stop must be pickup or delivery.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}

public sealed class MarkArrivedHandler(IAppDbContext db, ITenantContext tenant, IAuditLog audit, IRealtimeNotifier notifier, TimeProvider clock)
    : ICommandHandler<MarkArrivedCommand, JobDto>
{
    public async ValueTask<JobDto> Handle(MarkArrivedCommand command, CancellationToken cancellationToken)
    {
        var job = await db.VisibleJobAsync(tenant, command.JobId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;

        if (command.Stop == WaitingTimePolicy.Pickup)
        {
            // Waiting at the pickup ends when the job starts, so it can only begin before that.
            if (job.Status != JobStatus.Planned)
            {
                return job.Status == JobStatus.Cancelled ? throw new BusinessRuleException("This job was cancelled.") : JobDto.From(job);
            }

            var at = PhoneClock.Resolve(command.ArrivedAt, job.CreatedAtUtc, now);
            if (job.PickupArrivedAtUtc is { } existing && existing <= at)
            {
                return JobDto.From(job);
            }

            (job.PickupArrivedAtUtc, job.PickupArrivalSource, job.PickupArrivalLatitude, job.PickupArrivalLongitude) =
                (at, ArrivalSource.Driver, command.Latitude, command.Longitude);
        }
        else
        {
            if (job.Status == JobStatus.Delivered && job.DeliveryArrivedAtUtc is not null)
            {
                return JobDto.From(job);
            }

            if (job.Status != JobStatus.InProgress)
            {
                throw new BusinessRuleException(job.Status == JobStatus.Planned ? "Start the job first." : "This job is closed.");
            }

            var at = PhoneClock.Resolve(command.ArrivedAt, job.StartedAtUtc ?? job.CreatedAtUtc, now);
            if (job.DeliveryArrivedAtUtc is { } existing && existing <= at)
            {
                return JobDto.From(job);
            }

            (job.DeliveryArrivedAtUtc, job.DeliveryArrivalSource, job.DeliveryArrivalLatitude, job.DeliveryArrivalLongitude) =
                (at, ArrivalSource.Driver, command.Latitude, command.Longitude);
        }

        audit.Record("Arrived", "Job", job.Id.ToString(), command.Stop);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.JobChangedAsync(job.OrganizationId, job.Id, cancellationToken);
        return JobDto.From(job);
    }
}

/// <summary>
/// Close the job with proof of delivery: at least one delivery photo or the receiver's signature, and for
/// refrigerated loads a temperature reading from the 30 minutes before hand-over.
/// </summary>
[RequiresPolicy(Policies.JobWork)]
public sealed record DeliverJobCommand(
    Guid JobId, string RecipientName, string? Note, double? Latitude, double? Longitude, DateTimeOffset? DeliveredAt = null)
    : ICommand<JobDto>;

public sealed class DeliverJobValidator : AbstractValidator<DeliverJobCommand>
{
    public DeliverJobValidator()
    {
        RuleFor(x => x.RecipientName).NotEmpty().WithMessage("Enter the name of the person who received the goods.").MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}

public sealed class DeliverJobHandler(
    IAppDbContext db, ITenantContext tenant, IAuditLog audit, IRealtimeNotifier notifier, TimeProvider clock, ILogger<DeliverJobHandler> logger)
    : ICommandHandler<DeliverJobCommand, JobDto>
{
    public async ValueTask<JobDto> Handle(DeliverJobCommand command, CancellationToken cancellationToken)
    {
        var job = await db.VisibleJobAsync(tenant, command.JobId, cancellationToken);
        switch (job.Status)
        {
            case JobStatus.Delivered:
                return JobDto.From(job);
            case JobStatus.Planned:
                throw new BusinessRuleException("Start the job before marking it delivered.");
            case JobStatus.Cancelled:
                throw new BusinessRuleException("This job was cancelled.");
        }

        if (!job.Documents.Any(d => d.Kind is JobDocumentKind.DeliveryPhoto or JobDocumentKind.Signature))
        {
            throw new BusinessRuleException("Add a delivery photo or the receiver's signature before confirming delivery.");
        }

        var deliveredAt = PhoneClock.Resolve(command.DeliveredAt, job.StartedAtUtc ?? job.CreatedAtUtc, clock.GetUtcNow().UtcDateTime);
        if (JobTemperaturePolicy.IsRefrigerated(job) && !JobTemperaturePolicy.HasRecentReading(job, deliveredAt))
        {
            throw new BusinessRuleException("Log the load temperature (photo of the display) before confirming delivery.");
        }

        job.Status = JobStatus.Delivered;
        job.DeliveredAtUtc = deliveredAt;
        job.RecipientName = command.RecipientName.Trim();
        job.DeliveryNote = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim();
        job.DeliveredLatitude = command.Latitude;
        job.DeliveredLongitude = command.Longitude;
        audit.Record("Deliver", "Job", job.Id.ToString(), $"Received by {job.RecipientName}");
        await db.SaveChangesAsync(cancellationToken);

        // Committed: now tell the office. A failed alert never undoes the delivery.
        await notifier.JobChangedAsync(job.OrganizationId, job.Id, cancellationToken);
        try
        {
            var who = job.CustomerName ?? job.Destination ?? "the customer";
            await notifier.AlertAsync(job.OrganizationId, JobAlerts.Delivered, $"Delivered to {who}",
                $"{job.Vehicle.Name} delivered{(job.Reference is null ? "" : $" order {job.Reference}")}, received by {job.RecipientName}.",
                job.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Delivery alert failed for job {JobId}", job.Id);
        }

        return JobDto.From(job);
    }
}
