using FluentValidation;
using OmniOps.Application.Queries;

namespace OmniOps.Application.Validators;

public class GetLatestTelemetryQueryValidator : AbstractValidator<GetLatestTelemetryQuery>
{
    public GetLatestTelemetryQueryValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty()
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("VehicleId must be a non-empty alphanumeric string.");
    }
}
