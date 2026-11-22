using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OmniOps.Domain.Billing;

namespace OmniOps.Infrastructure.Persistence.Configurations;

internal sealed class BillingProfileConfiguration : IEntityTypeConfiguration<BillingProfile>
{
    public void Configure(EntityTypeBuilder<BillingProfile> entity)
    {
        entity.HasKey(e => e.OrganizationId);
        entity.Property(e => e.LegalName).HasMaxLength(200);
        entity.Property(e => e.Address).HasMaxLength(500);
        entity.Property(e => e.TaxId).HasMaxLength(50);
        entity.Property(e => e.Iban).HasMaxLength(50);
        entity.Property(e => e.BankName).HasMaxLength(120);
        entity.Property(e => e.Email).HasMaxLength(200);
        entity.Property(e => e.Phone).HasMaxLength(50);
        entity.Property(e => e.Currency).HasMaxLength(3);
        entity.Property(e => e.InvoicePrefix).HasMaxLength(12);
        entity.Property(e => e.TimeZone).HasMaxLength(64);
        entity.Property(e => e.DefaultVatRatePercent).HasPrecision(5, 2);
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> entity)
    {
        entity.HasIndex(e => new { e.OrganizationId, e.NormalizedName }).IsUnique();
        entity.Property(e => e.Name).HasMaxLength(200);
        entity.Property(e => e.NormalizedName).HasMaxLength(200);
        entity.Property(e => e.Email).HasMaxLength(200);
        entity.Property(e => e.Address).HasMaxLength(500);
        entity.Property(e => e.TaxId).HasMaxLength(50);
    }
}

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> entity)
    {
        entity.HasIndex(e => new { e.OrganizationId, e.Number }).IsUnique();
        entity.HasIndex(e => e.PublicToken).IsUnique();
        entity.HasIndex(e => new { e.Status, e.DueDateUtc });
        entity.HasIndex(e => e.JobId).IsUnique();
        entity.Property(e => e.Number).HasMaxLength(40);
        entity.Property(e => e.Currency).HasMaxLength(3);
        entity.Property(e => e.PublicToken).HasMaxLength(64);
        entity.Property(e => e.SellerName).HasMaxLength(200);
        entity.Property(e => e.SellerAddress).HasMaxLength(500);
        entity.Property(e => e.SellerTaxId).HasMaxLength(50);
        entity.Property(e => e.SellerIban).HasMaxLength(50);
        entity.Property(e => e.SellerBankName).HasMaxLength(120);
        entity.Property(e => e.SellerEmail).HasMaxLength(200);
        entity.Property(e => e.CustomerName).HasMaxLength(200);
        entity.Property(e => e.CustomerEmail).HasMaxLength(200);
        entity.Property(e => e.CustomerAddress).HasMaxLength(500);
        entity.Property(e => e.CustomerTaxId).HasMaxLength(50);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.HasOne(e => e.Job).WithMany().HasForeignKey(e => e.JobId).OnDelete(DeleteBehavior.SetNull);
        entity.HasMany(e => e.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        entity.HasMany(e => e.Reminders).WithOne().HasForeignKey(r => r.InvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> entity)
    {
        entity.Property(e => e.Description).HasMaxLength(300);
        entity.Property(e => e.Quantity).HasPrecision(12, 3);
        entity.Property(e => e.VatRatePercent).HasPrecision(5, 2);
    }
}

internal sealed class InvoiceReminderConfiguration : IEntityTypeConfiguration<InvoiceReminder>
{
    // A reminder of each kind goes out once, even if two workers race.
    public void Configure(EntityTypeBuilder<InvoiceReminder> entity) => entity.HasIndex(e => new { e.InvoiceId, e.Kind }).IsUnique();
}
