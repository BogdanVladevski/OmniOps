using Microsoft.Extensions.Logging.Abstractions;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Infrastructure.Services;

namespace OmniOps.Infrastructure.Tests.Services;

public class InMemoryAnomalyDetectionServiceTests
{
    private static readonly DateTime T0 = new(2026, 11, 21, 8, 0, 0, DateTimeKind.Utc);

    private static InMemoryAnomalyDetectionService CreateService() =>
        new(NullLogger<InMemoryAnomalyDetectionService>.Instance);

    private static Shipment CreateShipment(Guid? fleetId = null) => new()
    {
        Id = Guid.NewGuid(),
        FleetId = fleetId ?? Guid.NewGuid(),
        VehicleId = "Truck-001",
        ProductName = "Insulin",
        BatchNumber = "B-1",
        MinSafeTempCelsius = 2,
        MaxSafeTempCelsius = 8,
        Status = ShipmentStatus.InTransit,
    };

    private static VehicleTelemetry Reading(double temp, DateTime at) => new()
    {
        Id = Guid.NewGuid(),
        VehicleId = "Truck-001",
        EngineTemperature = temp,
        Timestamp = at,
    };

    [Fact]
    public async Task OutOfRangeReading_IsAlwaysFlagged()
    {
        var result = await CreateService().AnalyzeTelemetryAsync(Reading(12, T0), CreateShipment());

        Assert.True(result.IsAnomaly);
        Assert.Equal(0, result.ExcursionDurationSeconds);
    }

    [Fact]
    public async Task ContinuedExcursion_AccumulatesDurationUntilCritical()
    {
        var service = CreateService();
        var shipment = CreateShipment();
        var later = T0.AddSeconds(AnomalyClassifier.CriticalExcursionThresholdSeconds + 30);

        await service.AnalyzeTelemetryAsync(Reading(12, T0), shipment);
        var result = await service.AnalyzeTelemetryAsync(Reading(12, later), shipment);

        Assert.True(result.IsAnomaly);
        Assert.Equal(AnomalySeverity.Critical, result.Severity);
        Assert.Equal(AnomalyClassifier.CriticalExcursionThresholdSeconds + 30, result.ExcursionDurationSeconds);
    }

    [Fact]
    public async Task ReturnToRange_ResetsExcursionTimer()
    {
        var service = CreateService();
        var shipment = CreateShipment();

        await service.AnalyzeTelemetryAsync(Reading(12, T0), shipment);
        await service.AnalyzeTelemetryAsync(Reading(5, T0.AddMinutes(1)), shipment);
        var result = await service.AnalyzeTelemetryAsync(Reading(12, T0.AddMinutes(30)), shipment);

        Assert.Equal(0, result.ExcursionDurationSeconds);
    }

    [Fact]
    public async Task SameVehicleIdInAnotherFleet_DoesNotShareExcursionState()
    {
        var service = CreateService();

        await service.AnalyzeTelemetryAsync(Reading(12, T0), CreateShipment());
        var other = await service.AnalyzeTelemetryAsync(Reading(12, T0.AddMinutes(30)), CreateShipment());

        Assert.Equal(0, other.ExcursionDurationSeconds);
    }

    [Fact]
    public async Task WithoutShipment_NothingIsFlagged()
    {
        var result = await CreateService().AnalyzeTelemetryAsync(Reading(40, T0));

        Assert.False(result.IsAnomaly);
    }
}
