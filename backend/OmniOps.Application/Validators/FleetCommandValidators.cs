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
        // Truck names travel with every GPS point, which only accepts these characters.
        RuleFor(x => x.ExternalId).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("Use letters, numbers, - or _ for the truck name (e.g. Truck-1 or SK1234AB).");
        RuleFor(x => x.Registration).MaximumLength(30);
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
