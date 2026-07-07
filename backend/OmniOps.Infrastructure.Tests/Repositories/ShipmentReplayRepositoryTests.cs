using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Infrastructure.Data;
using OmniOps.Infrastructure.Services;
using Testcontainers.PostgreSql;

namespace OmniOps.Infrastructure.Tests.Repositories;

public class ShipmentReplayRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private AppDbContext _context = null!;
    private TelemetryRepository _repository = null!;

    private static readonly Guid ShipmentId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly DateTime WindowStart = new(2026, 7, 5, 11, 55, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 7, 5, 12, 2, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.MigrateAsync();
        _repository = new TelemetryRepository(_context);

        await SeedTelemetryAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task GetByVehicleInTimeRangeAsync_ReturnsRowsInTimestampOrder_BoundedToWindow()
    {
        var results = await _repository.GetByVehicleInTimeRangeAsync(
            "Truck-001",
            WindowStart,
            WindowEnd);

        Assert.Equal(3, results.Count);
        Assert.True(results.SequenceEqual(results.OrderBy(point => point.Timestamp)));
        Assert.All(results, point =>
        {
            Assert.Equal("Truck-001", point.VehicleId);
            Assert.InRange(point.Timestamp, WindowStart, WindowEnd);
        });
        Assert.Equal(88, results[0].EngineTemperature);
        Assert.Equal(102, results[1].EngineTemperature);
        Assert.Equal(101, results[2].EngineTemperature);
    }

    [Fact]
    public async Task GetByVehicleInTimeRangeAsync_ExcludesRowsOutsideWindow()
    {
        var results = await _repository.GetByVehicleInTimeRangeAsync(
            "Truck-001",
            WindowStart.AddMinutes(1),
            WindowEnd.AddMinutes(-1));

        Assert.Single(results);
        Assert.Equal(102, results[0].EngineTemperature);
    }

    private async Task SeedTelemetryAsync()
    {
        _context.Telemetries.AddRange(
            new VehicleTelemetry
            {
                Id = Guid.NewGuid(),
                VehicleId = "Truck-001",
                Latitude = 42.0,
                Longitude = 21.4,
                Speed = 60,
                FuelLevel = 80,
                EngineTemperature = 88,
                Timestamp = WindowStart.AddSeconds(30)
            },
            new VehicleTelemetry
            {
                Id = Guid.NewGuid(),
                VehicleId = "Truck-001",
                Latitude = 42.01,
                Longitude = 21.41,
                Speed = 65,
                FuelLevel = 79,
                EngineTemperature = 102,
                Timestamp = WindowStart.AddMinutes(3)
            },
            new VehicleTelemetry
            {
                Id = Guid.NewGuid(),
                VehicleId = "Truck-001",
                Latitude = 42.02,
                Longitude = 21.42,
                Speed = 70,
                FuelLevel = 78,
                EngineTemperature = 101,
                Timestamp = WindowEnd
            },
            new VehicleTelemetry
            {
                Id = Guid.NewGuid(),
                VehicleId = "Truck-001",
                Latitude = 42.03,
                Longitude = 21.43,
                Speed = 72,
                FuelLevel = 77,
                EngineTemperature = 99,
                Timestamp = WindowEnd.AddMinutes(5)
            },
            new VehicleTelemetry
            {
                Id = Guid.NewGuid(),
                VehicleId = "Truck-002",
                Latitude = 41.9,
                Longitude = 21.3,
                Speed = 55,
                FuelLevel = 90,
                EngineTemperature = 90,
                Timestamp = WindowStart.AddMinutes(2)
            });

        await _context.SaveChangesAsync();
    }
}
