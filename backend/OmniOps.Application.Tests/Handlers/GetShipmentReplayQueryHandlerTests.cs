using NSubstitute;
using OmniOps.Application.Queries;
using OmniOps.Application.Queries.Handlers;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Tests.Handlers;

public class GetShipmentReplayQueryHandlerTests
{
    private readonly IShipmentRepository _shipmentRepository = Substitute.For<IShipmentRepository>();
    private readonly ITelemetryRepository _telemetryRepository = Substitute.For<ITelemetryRepository>();
    private readonly GetShipmentReplayQueryHandler _handler;

    private static readonly Guid ShipmentId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly DateTime Anchor = new(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc);

    public GetShipmentReplayQueryHandlerTests()
    {
        _handler = new GetShipmentReplayQueryHandler(_shipmentRepository, _telemetryRepository);
    }

    [Fact]
    public async Task Handle_WhenShipmentMissing_ReturnsNull()
    {
        _shipmentRepository.GetByIdAsync(ShipmentId, Arg.Any<CancellationToken>())
            .Returns((Shipment?)null);

        var result = await _handler.Handle(
            new GetShipmentReplayQuery(ShipmentId, null, null, Anchor),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithAnchorUtc_ReturnsOrderedReplayPoints()
    {
        var shipment = new Shipment
        {
            Id = ShipmentId,
            VehicleId = "Truck-001",
            ProductName = "Insulin Glargine",
            BatchNumber = "B-4471",
            MinSafeTempCelsius = 50,
            MaxSafeTempCelsius = 100,
            ValueAtRiskUsd = 12_400m,
            Status = ShipmentStatus.InTransit
        };

        var fromUtc = Anchor.AddMinutes(-5);
        var toUtc = Anchor.AddMinutes(2);

        var points = new List<VehicleTelemetry>
        {
            CreateTelemetry(fromUtc.AddMinutes(1), 88),
            CreateTelemetry(fromUtc.AddMinutes(3), 102),
            CreateTelemetry(toUtc, 101)
        };

        _shipmentRepository.GetByIdAsync(ShipmentId, Arg.Any<CancellationToken>())
            .Returns(shipment);
        _telemetryRepository
            .GetByVehicleInTimeRangeAsync("Truck-001", fromUtc, toUtc, Arg.Any<CancellationToken>())
            .Returns(points);

        var result = await _handler.Handle(
            new GetShipmentReplayQuery(ShipmentId, null, null, Anchor),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, result!.Points.Count);
        Assert.Equal(fromUtc, result.FromUtc);
        Assert.Equal(toUtc, result.ToUtc);
        Assert.Equal("Insulin Glargine", result.ProductName);
        Assert.Equal(102, result.Points[1].EngineTemperature);
        Assert.Equal(ShipmentId, result.Points[0].Shipment!.ShipmentId);
    }

    private static VehicleTelemetry CreateTelemetry(DateTime timestamp, double temperature) => new()
    {
        Id = Guid.NewGuid(),
        VehicleId = "Truck-001",
        Latitude = 42.0,
        Longitude = 21.4,
        Speed = 70,
        FuelLevel = 80,
        EngineTemperature = temperature,
        Timestamp = timestamp
    };
}
