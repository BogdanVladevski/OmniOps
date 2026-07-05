using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;

namespace OmniOps.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<VehicleTelemetry> Telemetries => Set<VehicleTelemetry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Shipment> Shipments => Set<Shipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VehicleTelemetry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.VehicleId, e.Timestamp });
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProcessedOnUtc);
            entity.HasIndex(e => new { e.ProcessedOnUtc, e.OccurredOnUtc });
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.VehicleId, e.Status });
            entity.Property(e => e.ValueAtRiskUsd).HasPrecision(18, 2);

            // Seed 3 shipments matching the Truck-001/002/003 fleet.
            // Safe temp ranges are intentionally aligned to the simulators EngineTemperature
            // output (85–105 °C) so that excursions fire realistically during demos.
            entity.HasData(
                new Shipment
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
                },
                new Shipment
                {
                    Id = new Guid("a2000000-0000-0000-0000-000000000002"),
                    VehicleId = "Truck-002",
                    ProductName = "Hepatitis B Vaccine",
                    BatchNumber = "HBV-0293",
                    MinSafeTempCelsius = 50.0,
                    MaxSafeTempCelsius = 95.0,
                    ValueAtRiskUsd = 8_750m,
                    DepartedAtUtc = new DateTime(2026, 7, 2, 6, 30, 0, DateTimeKind.Utc),
                    ExpectedDeliveryUtc = new DateTime(2026, 7, 8, 14, 0, 0, DateTimeKind.Utc),
                    Status = ShipmentStatus.InTransit
                },
                new Shipment
                {
                    Id = new Guid("a3000000-0000-0000-0000-000000000003"),
                    VehicleId = "Truck-003",
                    ProductName = "BCG Vaccine",
                    BatchNumber = "BCG-1182",
                    MinSafeTempCelsius = 55.0,
                    MaxSafeTempCelsius = 100.0,
                    ValueAtRiskUsd = 6_200m,
                    DepartedAtUtc = new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc),
                    ExpectedDeliveryUtc = new DateTime(2026, 7, 9, 20, 0, 0, DateTimeKind.Utc),
                    Status = ShipmentStatus.InTransit
                }
            );
        });
    }
}
