using FluentValidation;
using OmniOps.Application.Queries;

namespace OmniOps.Application.Validators;

public class GetShipmentReplayQueryValidator : AbstractValidator<GetShipmentReplayQuery>
{
    public const int MaxWindowMinutes = 30;

    public GetShipmentReplayQueryValidator()
    {
        RuleFor(x => x.ShipmentId)
            .NotEmpty();

        RuleFor(x => x)
            .Must(HasResolvableTimeWindow)
            .WithMessage("Provide fromUtc and toUtc, or anchorUtc to derive the replay window.");

        When(x => x.FromUtc.HasValue && x.ToUtc.HasValue, () =>
        {
            RuleFor(x => x.ToUtc)
                .GreaterThan(x => x.FromUtc!.Value)
                .WithMessage("toUtc must be after fromUtc.");

            RuleFor(x => x)
                .Must(query => (query.ToUtc!.Value - query.FromUtc!.Value).TotalMinutes <= MaxWindowMinutes)
                .WithMessage($"Replay window cannot exceed {MaxWindowMinutes} minutes.");
        });
    }

    private static bool HasResolvableTimeWindow(GetShipmentReplayQuery query) =>
        (query.FromUtc.HasValue && query.ToUtc.HasValue) || query.AnchorUtc.HasValue;
}
