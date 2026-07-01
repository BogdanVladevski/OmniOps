using FluentValidation;
using OmniOps.Application.Commands;

namespace OmniOps.Application.Validators;

public class ProcessTelemetryCommandValidator : AbstractValidator<ProcessTelemetryCommand>
{
    public ProcessTelemetryCommandValidator()
    {
        RuleFor(x => x.Telemetry)
            .NotNull()
            .WithMessage("Telemetry payload is required.");

        RuleFor(x => x.Telemetry.VehicleId)
            .NotEmpty()
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("VehicleId must be a non-empty alphanumeric string.");

        RuleFor(x => x.Telemetry.Latitude)
            .InclusiveBetween(-90, 90);

        RuleFor(x => x.Telemetry.Longitude)
            .InclusiveBetween(-180, 180);

        RuleFor(x => x.Telemetry.Speed)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Telemetry.FuelLevel)
            .InclusiveBetween(0, 100);

        RuleFor(x => x.Telemetry.EngineTemperature)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Telemetry.Timestamp)
            .Must(timestamp => timestamp != default)
            .WithMessage("Timestamp is required.");
    }
}
