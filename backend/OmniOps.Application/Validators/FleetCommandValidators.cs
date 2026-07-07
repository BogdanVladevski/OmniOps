using FluentValidation;
using OmniOps.Application.Commands;

namespace OmniOps.Application.Validators;

public class CreateFleetCommandValidator : AbstractValidator<CreateFleetCommand>
{
    public CreateFleetCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(x => x.FleetId).NotEmpty();
        RuleFor(x => x.ExternalId).NotEmpty().MaximumLength(50);
    }
}

public class CreateGeofenceCommandValidator : AbstractValidator<CreateGeofenceCommand>
{
    public CreateGeofenceCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShapeType).NotEmpty();
    }
}

public class ReplayEventsCommandValidator : AbstractValidator<ReplayEventsCommand>
{
    public ReplayEventsCommandValidator()
    {
        RuleFor(x => x.ToUtc).GreaterThan(x => x.FromUtc);
        RuleFor(x => x.FromUtc).LessThan(DateTime.UtcNow.AddDays(1));
    }
}
