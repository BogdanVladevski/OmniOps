using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;

namespace OmniOps.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<VehicleTelemetry> Telemetries => Set<VehicleTelemetry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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
    }
}
