using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OmniOps.Domain.Fleet;

namespace OmniOps.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> entity)
    {
        entity.HasIndex(e => new { e.OrganizationId, e.Name }).IsUnique();
        entity.Property(e => e.Name).HasMaxLength(50);
        entity.Property(e => e.Registration).HasMaxLength(30);
    }
}

internal sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> entity)
    {
        entity.HasIndex(e => e.OrganizationId);
        entity.Property(e => e.FullName).HasMaxLength(200);
        entity.Property(e => e.Phone).HasMaxLength(40);
    }
}
