using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OmniOps.Domain.Accounts;
using OmniOps.Domain.Common;

namespace OmniOps.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> entity)
    {
        entity.HasIndex(e => e.Slug).IsUnique();
        entity.Property(e => e.Name).HasMaxLength(200);
        entity.Property(e => e.Slug).HasMaxLength(60);
    }
}

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> entity)
    {
        entity.ToTable("Users");
        entity.HasIndex(e => e.Email).IsUnique();
        entity.HasIndex(e => new { e.OrganizationId, e.DriverId });
        entity.Property(e => e.Email).HasMaxLength(200);
        entity.Property(e => e.DisplayName).HasMaxLength(200);
        entity.Property(e => e.PasswordHash).HasMaxLength(200);
    }
}

internal sealed class DriverPairingCodeConfiguration : IEntityTypeConfiguration<DriverPairingCode>
{
    public void Configure(EntityTypeBuilder<DriverPairingCode> entity)
    {
        entity.HasIndex(e => e.CodeHash).IsUnique();
        entity.Property(e => e.CodeHash).HasMaxLength(64);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(64);
    }
}

internal sealed class AccountLinkConfiguration : IEntityTypeConfiguration<AccountLink>
{
    public void Configure(EntityTypeBuilder<AccountLink> entity)
    {
        entity.HasIndex(e => e.TokenHash).IsUnique();
        entity.HasIndex(e => new { e.OrganizationId, e.Purpose });
        entity.Property(e => e.TokenHash).HasMaxLength(64);
        entity.Property(e => e.Email).HasMaxLength(200);
        entity.Property(e => e.CreatedByUserId).HasMaxLength(64);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
    {
        entity.HasIndex(e => e.TokenHash).IsUnique();
        entity.HasIndex(e => e.UserId);
        entity.Property(e => e.TokenHash).HasMaxLength(64);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> entity)
    {
        entity.HasIndex(e => new { e.OrganizationId, e.OccurredAtUtc });
        entity.Property(e => e.Action).HasMaxLength(60);
        entity.Property(e => e.EntityType).HasMaxLength(60);
        entity.Property(e => e.EntityId).HasMaxLength(64);
        entity.Property(e => e.Details).HasMaxLength(500);
    }
}
