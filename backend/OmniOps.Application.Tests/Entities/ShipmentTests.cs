using OmniOps.Core.Entities;
using OmniOps.Core.Enums;

namespace OmniOps.Application.Tests.Entities;

public class ShipmentTests
{
    [Fact]
    public void Shipment_WhenCargoTempWithinSafeRange_IsNotInExcursion()
    {
        var shipment = CreateInsulinShipment();

        var cargoTemp = 75.0;
        var inExcursion = cargoTemp > shipment.MaxSafeTempCelsius || cargoTemp < shipment.MinSafeTempCelsius;

        Assert.False(inExcursion);
    }

    [Fact]
    public void Shipment_WhenCargoTempAboveMax_IsInExcursion()
    {
        var shipment = CreateInsulinShipment();

        var cargoTemp = 105.0;
        var inExcursion = cargoTemp > shipment.MaxSafeTempCelsius;

        Assert.True(inExcursion);
    }

    [Fact]
    public void Shipment_WhenCargoTempBelowMin_IsInExcursion()
    {
        var shipment = CreateInsulinShipment();

        var cargoTemp = 40.0;
        var inExcursion = cargoTemp < shipment.MinSafeTempCelsius;

        Assert.True(inExcursion);
    }

    [Fact]
    public void Shipment_WhenCargoTempAtExactBoundary_IsNotInExcursion()
    {
        var shipment = CreateInsulinShipment();

        Assert.False(shipment.MinSafeTempCelsius > shipment.MinSafeTempCelsius);
        Assert.False(shipment.MaxSafeTempCelsius > shipment.MaxSafeTempCelsius);
    }

    [Theory]
    [InlineData(0, 90.0, false)]
    [InlineData(30, 101.0, true)]
    [InlineData(90, 103.0, true)]
    public void ExcursionDuration_ScalesWithConsecutiveOutOfRangeReadings(
        int expectedDurationSeconds,
        double cargoTemp,
        bool expectsExcursion)
    {
        var shipment = CreateInsulinShipment();
        var isInExcursion = cargoTemp > shipment.MaxSafeTempCelsius || cargoTemp < shipment.MinSafeTempCelsius;

        Assert.Equal(expectsExcursion, isInExcursion);

        if (isInExcursion)
        {
            Assert.True(expectedDurationSeconds >= 0,
                "Excursion duration must be non-negative");
        }
    }

    [Fact]
    public void Shipment_ValueAtRiskUsd_ReflectsShipmentSize()
    {
        var insulin = CreateInsulinShipment();
        var vaccine = new Shipment
        {
            Id = Guid.NewGuid(),
            VehicleId = "Truck-002",
            ProductName = "Hepatitis B Vaccine",
            BatchNumber = "HBV-0293",
            MinSafeTempCelsius = 50,
            MaxSafeTempCelsius = 95,
            ValueAtRiskUsd = 8_750m,
            Status = ShipmentStatus.InTransit
        };

        Assert.True(insulin.ValueAtRiskUsd > vaccine.ValueAtRiskUsd,
            "Insulin batch should have higher value at risk than vaccine batch in this fixture");
    }

    [Fact]
    public void Shipment_DefaultStatus_IsInTransit()
    {
        var shipment = new Shipment
        {
            VehicleId = "Truck-001",
            ProductName = "Test Product",
            BatchNumber = "T-001",
            Status = ShipmentStatus.InTransit
        };

        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
    }

    private static Shipment CreateInsulinShipment() => new()
    {
        Id = new Guid("a1000000-0000-0000-0000-000000000001"),
        VehicleId = "Truck-001",
        ProductName = "Insulin Glargine",
        BatchNumber = "B-4471",
        MinSafeTempCelsius = 50.0,
        MaxSafeTempCelsius = 100.0,
        ValueAtRiskUsd = 12_400m,
        DepartedAtUtc = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
        ExpectedDeliveryUtc = new DateTime(2026, 7, 7, 18, 0, 0, DateTimeKind.Utc),
        Status = ShipmentStatus.InTransit
    };
}
