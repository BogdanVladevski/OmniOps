using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Data
{
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
            });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Capture and serialize domain events before saving
            var outboxMessages = ChangeTracker.Entries<IHasDomainEvents>()
                .Select(entry => entry.Entity)
                .SelectMany(entity =>
                {
                    var events = entity.DomainEvents.ToList();
                    entity.ClearDomainEvents();
                    return events;
                })
                .Select(domainEvent => new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    OccurredOnUtc = DateTime.UtcNow,
                    Type = domainEvent.GetType().Name,
                    Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType())
                })
                .ToList();

            if (outboxMessages.Any())
            {
                await OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
