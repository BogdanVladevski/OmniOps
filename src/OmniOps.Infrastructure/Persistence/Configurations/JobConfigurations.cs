using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OmniOps.Domain.Jobs;

namespace OmniOps.Infrastructure.Persistence.Configurations;

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> entity)
    {
        entity.HasIndex(e => new { e.OrganizationId, e.Status });
        entity.HasIndex(e => new { e.OrganizationId, e.DriverId, e.Status });
        entity.HasIndex(e => e.TrackingToken).IsUnique();
        entity.Property(e => e.CustomerName).HasMaxLength(200);
        entity.Property(e => e.Reference).HasMaxLength(100);
        entity.Property(e => e.Origin).HasMaxLength(300);
        entity.Property(e => e.Destination).HasMaxLength(300);
        entity.Property(e => e.TrackingToken).HasMaxLength(64);
        entity.Property(e => e.RecipientName).HasMaxLength(200);
        entity.Property(e => e.DeliveryNote).HasMaxLength(1000);
        entity.HasOne(e => e.Vehicle).WithMany().HasForeignKey(e => e.VehicleId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Driver).WithMany().HasForeignKey(e => e.DriverId).OnDelete(DeleteBehavior.SetNull);
        entity.HasMany(e => e.Documents).WithOne(d => d.Job).HasForeignKey(d => d.JobId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class JobDocumentConfiguration : IEntityTypeConfiguration<JobDocument>
{
    public void Configure(EntityTypeBuilder<JobDocument> entity)
    {
        // A phone resending the same upload must not create a second record.
        entity.HasIndex(e => new { e.JobId, e.ClientId }).IsUnique();
        entity.Property(e => e.StorageKey).HasMaxLength(300);
        entity.Property(e => e.ContentType).HasMaxLength(100);
        entity.Property(e => e.Note).HasMaxLength(500);
    }
}

internal sealed class JobLocationConfiguration : IEntityTypeConfiguration<JobLocation>
{
    public void Configure(EntityTypeBuilder<JobLocation> entity)
    {
        // The id comes from the phone; resent readings are skipped.
        entity.Property(e => e.Id).ValueGeneratedNever();
        entity.HasIndex(e => new { e.JobId, e.RecordedAtUtc });
        entity.HasOne<Job>().WithMany().HasForeignKey(e => e.JobId).OnDelete(DeleteBehavior.Cascade);
    }
}
