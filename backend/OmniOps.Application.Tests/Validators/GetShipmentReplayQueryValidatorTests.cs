using FluentValidation.TestHelper;
using OmniOps.Application.Queries;
using OmniOps.Application.Validators;

namespace OmniOps.Application.Tests.Validators;

public class GetShipmentReplayQueryValidatorTests
{
    private readonly GetShipmentReplayQueryValidator _validator = new();
    private static readonly Guid ShipmentId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly DateTime Anchor = new(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Validate_WithExplicitWindow_Succeeds()
    {
        var result = _validator.TestValidate(new GetShipmentReplayQuery(
            ShipmentId,
            Anchor.AddMinutes(-5),
            Anchor.AddMinutes(2),
            null));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithAnchorUtc_Succeeds()
    {
        var result = _validator.TestValidate(new GetShipmentReplayQuery(
            ShipmentId,
            null,
            null,
            Anchor));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithoutTimeWindow_Fails()
    {
        var result = _validator.TestValidate(new GetShipmentReplayQuery(
            ShipmentId,
            null,
            null,
            null));

        result.ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Validate_WithWindowExceedingMax_Fails()
    {
        var result = _validator.TestValidate(new GetShipmentReplayQuery(
            ShipmentId,
            Anchor,
            Anchor.AddMinutes(GetShipmentReplayQueryValidator.MaxWindowMinutes + 1),
            null));

        result.ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Validate_WithInvertedWindow_Fails()
    {
        var result = _validator.TestValidate(new GetShipmentReplayQuery(
            ShipmentId,
            Anchor.AddMinutes(5),
            Anchor,
            null));

        result.ShouldHaveValidationErrorFor(x => x.ToUtc);
    }
}
