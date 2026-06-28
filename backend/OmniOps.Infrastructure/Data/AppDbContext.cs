using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;

namespace OmniOps.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<VehicleTelemetry> Telemetries => Set<VehicleTelemetry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<VehicleTelemetry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.VehicleId, e.Timestamp });
            });
        }
    }
}
