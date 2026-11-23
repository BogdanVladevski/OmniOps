using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Domain.Common;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Jobs;

[RequiresPolicy(Policies.Office)]
public sealed record CreateJobCommand(
    Guid VehicleId,
    Guid? DriverId,
    string CustomerName,
    string? Reference,
    string? Origin,
    double? OriginLatitude,
    double? OriginLongitude,
    string Destination,
    double? DestinationLatitude,
    double? DestinationLongitude,
    DateTimeOffset? PlannedDeliveryUtc,
    double? MinTempCelsius,
    double? MaxTempCelsius) : ICommand<JobDto>;

public sealed class CreateJobValidator : AbstractValidator<CreateJobCommand>
{
    public CreateJobValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("Choose a truck.");
        RuleFor(x => x.CustomerName).NotEmpty().WithMessage("Who is the delivery for?").MaximumLength(200);
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.Origin).MaximumLength(300);
        RuleFor(x => x.Destination).NotEmpty().WithMessage("Where does it go?").MaximumLength(300);
        RuleFor(x => x.OriginLatitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.OriginLongitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.DestinationLatitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.DestinationLongitude).InclusiveBetween(-180, 180);
        RuleFor(x => x).Must(x => x.OriginLatitude.HasValue == x.OriginLongitude.HasValue)
            .WithMessage("Give both pickup coordinates or neither.");
        RuleFor(x => x).Must(x => x.DestinationLatitude.HasValue == x.DestinationLongitude.HasValue)
            .WithMessage("Give both destination coordinates or neither.");
        RuleFor(x => x).Must(x => x.MinTempCelsius.HasValue == x.MaxTempCelsius.HasValue)
            .WithMessage("Give both ends of the temperature range or neither.");
        RuleFor(x => x).Must(x => x.MinTempCelsius is null || x.MinTempCelsius < x.MaxTempCelsius)
            .WithMessage("The lowest temperature must be below the highest.");
        RuleFor(x => x.MinTempCelsius).InclusiveBetween(JobTemperaturePolicy.MinPlausibleCelsius, JobTemperaturePolicy.MaxPlausibleCelsius);
        RuleFor(x => x.MaxTempCelsius).InclusiveBetween(JobTemperaturePolicy.MinPlausibleCelsius, JobTemperaturePolicy.MaxPlausibleCelsius);
    }
}

public sealed class CreateJobHandler(IAppDbContext db, ITenantContext tenant, IAuditLog audit, IRealtimeNotifier notifier, TimeProvider clock)
    : ICommandHandler<CreateJobCommand, JobDto>
{
    public async ValueTask<JobDto> Handle(CreateJobCommand command, CancellationToken cancellationToken)
    {
        // Another company's truck or driver is simply "not found" (the query filter only sees ours).
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == command.VehicleId && v.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Truck not found.");
        var driver = command.DriverId is { } driverId
            ? await db.Drivers.FirstOrDefaultAsync(d => d.Id == driverId && d.IsActive, cancellationToken)
              ?? throw new KeyNotFoundException("Driver not found.")
            : null;

        var job = new Job
        {
            Id = Guid.NewGuid(),
            OrganizationId = tenant.OrganizationId,
            VehicleId = vehicle.Id,
            DriverId = driver?.Id,
            Status = JobStatus.Planned,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            CustomerName = command.CustomerName.Trim(),
            Reference = command.Reference?.Trim(),
            Origin = command.Origin?.Trim(),
            OriginLatitude = command.OriginLatitude,
            OriginLongitude = command.OriginLongitude,
            Destination = command.Destination.Trim(),
            DestinationLatitude = command.DestinationLatitude,
            DestinationLongitude = command.DestinationLongitude,
            PlannedDeliveryUtc = command.PlannedDeliveryUtc?.UtcDateTime,
            MinTempCelsius = command.MinTempCelsius,
            MaxTempCelsius = command.MaxTempCelsius,
            TrackingToken = PublicToken.Create(),
            Vehicle = vehicle,
            Driver = driver,
        };

        db.Jobs.Add(job);
        audit.Record("Create", "Job", job.Id.ToString(), $"{job.CustomerName} → {job.Destination}");
        await db.SaveChangesAsync(cancellationToken);
        await notifier.JobChangedAsync(job.OrganizationId, job.Id, cancellationToken);
        return JobDto.From(job);
    }
}
