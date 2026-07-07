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
    public DbSet<StoredEvent> StoredEvents => Set<StoredEvent>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<Fleet> Fleets => Set<Fleet>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Depot> Depots => Set<Depot>();
    public DbSet<VehicleMaintenanceRecord> VehicleMaintenanceRecords => Set<VehicleMaintenanceRecord>();
    public DbSet<Geofence> Geofences => Set<Geofence>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentNote> IncidentNotes => Set<IncidentNote>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VehicleTelemetry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.VehicleId, e.Timestamp });
        });

        modelBuilder.Entity<StoredEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OccurredOnUtc);
            entity.HasIndex(e => new { e.AggregateType, e.AggregateId });
            entity.HasIndex(e => e.EventType);
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

        modelBuilder.Entity<Fleet>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.OrganizationId);
            entity.HasData(new Fleet
            {
                Id = new Guid("f1000000-0000-0000-0000-000000000001"),
                OrganizationId = TenantSeed.DefaultOrganizationId,
                WorkspaceId = TenantSeed.DefaultWorkspaceId,
                Name = "Cold-Chain North",
                Description = "Primary pharmaceutical cold-chain fleet",
                CreatedAtUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.FleetId, e.ExternalId }).IsUnique();
            entity.HasOne(e => e.Fleet).WithMany(f => f.Vehicles).HasForeignKey(e => e.FleetId);
            entity.HasOne(e => e.AssignedDriver).WithMany().HasForeignKey(e => e.AssignedDriverId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasData(
                new Vehicle
                {
                    Id = new Guid("b1000000-0000-0000-0000-000000000001"),
                    FleetId = new Guid("f1000000-0000-0000-0000-000000000001"),
                    ExternalId = "Truck-001",
                    Vin = "1HGBH41JXMN109186",
                    Registration = "CC-4471",
                    Status = VehicleOperationalStatus.Active
                },
                new Vehicle
                {
                    Id = new Guid("b2000000-0000-0000-0000-000000000002"),
                    FleetId = new Guid("f1000000-0000-0000-0000-000000000001"),
                    ExternalId = "Truck-002",
                    Vin = "1HGBH41JXMN109187",
                    Registration = "CC-0293",
                    Status = VehicleOperationalStatus.Active
                },
                new Vehicle
                {
                    Id = new Guid("b3000000-0000-0000-0000-000000000003"),
                    FleetId = new Guid("f1000000-0000-0000-0000-000000000001"),
                    ExternalId = "Truck-003",
                    Vin = "1HGBH41JXMN109188",
                    Registration = "CC-1182",
                    Status = VehicleOperationalStatus.Active
                });
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Fleet).WithMany(f => f.Drivers).HasForeignKey(e => e.FleetId);
        });

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.VehicleId, e.Status });
            entity.HasOne(e => e.Vehicle).WithMany(v => v.Trips).HasForeignKey(e => e.VehicleId);
            entity.HasOne(e => e.Driver).WithMany().HasForeignKey(e => e.DriverId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Depot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Fleet).WithMany(f => f.Depots).HasForeignKey(e => e.FleetId);
        });

        modelBuilder.Entity<VehicleMaintenanceRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Vehicle).WithMany(v => v.MaintenanceHistory).HasForeignKey(e => e.VehicleId);
        });

        modelBuilder.Entity<Geofence>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FleetId);
            entity.HasOne(e => e.Fleet).WithMany().HasForeignKey(e => e.FleetId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasData(new Geofence
            {
                Id = new Guid("a4000000-0000-0000-0000-000000000001"),
                FleetId = new Guid("f1000000-0000-0000-0000-000000000001"),
                Name = "Skopje Distribution Hub",
                ShapeType = GeofenceShapeType.Radius,
                CenterLatitude = 41.9965,
                CenterLongitude = 21.4314,
                RadiusMeters = 2500,
                IsActive = true
            });
        });

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.FleetId, e.Status });
            entity.HasIndex(e => e.VehicleId);
            entity.HasIndex(e => e.OrganizationId);
        });

        modelBuilder.Entity<IncidentNote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Incident).WithMany(i => i.Notes).HasForeignKey(e => e.IncidentId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OccurredAtUtc);
        });

        modelBuilder.Entity<LoginHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasData(new Organization
            {
                Id = TenantSeed.DefaultOrganizationId,
                Name = "OmniOps Demo Org",
                Slug = "omniops-demo",
                CreatedAtUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrganizationId, e.Name }).IsUnique();
            entity.HasOne(e => e.Organization).WithMany(o => o.Workspaces).HasForeignKey(e => e.OrganizationId);
            entity.HasData(new Workspace
            {
                Id = TenantSeed.DefaultWorkspaceId,
                OrganizationId = TenantSeed.DefaultOrganizationId,
                Name = "North America Operations",
                DefaultFleetId = new Guid("f1000000-0000-0000-0000-000000000001"),
                CreatedAtUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Organization).WithMany(o => o.Teams).HasForeignKey(e => e.OrganizationId);
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TeamId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Team).WithMany(t => t.Members).HasForeignKey(e => e.TeamId);
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash);
            entity.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId);
        });

        modelBuilder.Entity<TenantSettings>(entity =>
        {
            entity.HasKey(e => e.OrganizationId);
            entity.HasOne(e => e.Organization).WithOne().HasForeignKey<TenantSettings>(e => e.OrganizationId);
            entity.HasData(new TenantSettings { OrganizationId = TenantSeed.DefaultOrganizationId });
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrganizationId, e.UserId, e.Status });
        });

        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrganizationId, e.UserId, e.AlertType }).IsUnique();
        });

        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrganizationId);
        });

        modelBuilder.Entity<DeviceRegistration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.PushToken }).IsUnique();
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.KeyPrefix);
            entity.HasIndex(e => e.OrganizationId);
        });
    }
}
