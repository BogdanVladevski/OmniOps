using FluentValidation.TestHelper;
using OmniOps.Application.Commands;
using OmniOps.Application.Queries;
using OmniOps.Application.Validators;
using OmniOps.Core.Entities;

namespace OmniOps.Application.Tests.Validators;

public class ProcessTelemetryCommandValidatorTests
{
    private readonly ProcessTelemetryCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidTelemetry_Succeeds()
    {
        var command = new ProcessTelemetryCommand(CreateValidTelemetry());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithInvalidVehicleId_Fails()
    {
        var telemetry = CreateValidTelemetry();
        telemetry.VehicleId = "invalid id!";
        var result = _validator.TestValidate(new ProcessTelemetryCommand(telemetry));
        result.ShouldHaveValidationErrorFor(x => x.Telemetry.VehicleId);
    }

    [Fact]
    public void Validate_WithOutOfRangeLatitude_Fails()
    {
        var telemetry = CreateValidTelemetry();
        telemetry.Latitude = 95;
        var result = _validator.TestValidate(new ProcessTelemetryCommand(telemetry));
        result.ShouldHaveValidationErrorFor(x => x.Telemetry.Latitude);
    }

    private static VehicleTelemetry CreateValidTelemetry() => new()
    {
        Id = Guid.NewGuid(),
        VehicleId = "Truck-001",
        Latitude = 41.99,
        Longitude = 21.43,
        Speed = 80,
        FuelLevel = 75,
        EngineTemperature = 90,
        Timestamp = DateTime.UtcNow
    };
}

public class GetLatestTelemetryQueryValidatorTests
{
    private readonly GetLatestTelemetryQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidVehicleId_Succeeds()
    {
        var result = _validator.TestValidate(new GetLatestTelemetryQuery("Truck-001"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyVehicleId_Fails()
    {
        var result = _validator.TestValidate(new GetLatestTelemetryQuery(""));
        result.ShouldHaveValidationErrorFor(x => x.VehicleId);
    }
}
