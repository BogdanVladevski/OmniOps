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

public class JobPaperworkValidatorTests
{
    private static OmniOps.Application.Commands.AddJobDocumentCommand Doc(OmniOps.Core.Enums.TripDocumentKind kind, double? celsius) =>
        new(Guid.NewGuid(), kind, new MemoryStream([1]), "image/jpeg", 1, null, null, celsius);

    [Fact]
    public void TemperaturePhoto_WithoutAReading_IsRejected()
    {
        new AddJobDocumentCommandValidator().TestValidate(Doc(OmniOps.Core.Enums.TripDocumentKind.TemperaturePhoto, null))
            .ShouldHaveValidationErrorFor(x => x.TemperatureCelsius);
    }

    [Fact]
    public void TemperaturePhoto_WithAnImplausibleReading_IsRejected()
    {
        // e.g. the driver typed 45 instead of 4.5
        new AddJobDocumentCommandValidator().TestValidate(Doc(OmniOps.Core.Enums.TripDocumentKind.TemperaturePhoto, 45))
            .ShouldHaveValidationErrorFor(x => x.TemperatureCelsius);
    }

    [Fact]
    public void DeliveryPhoto_NeedsNoReading()
    {
        new AddJobDocumentCommandValidator().TestValidate(Doc(OmniOps.Core.Enums.TripDocumentKind.DeliveryPhoto, null))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(2.0, null)]
    [InlineData(8.0, 2.0)]
    [InlineData(-60.0, 8.0)]
    public void CreateJob_WithAnIncompleteOrInvertedRange_IsRejected(double min, double? max)
    {
        var cmd = new CreateJobCommand(Guid.NewGuid(), null, "Customer", null, null, "Bitola", null, null, null, min, max);
        Assert.False(new CreateJobCommandValidator().Validate(cmd).IsValid);
    }
}
