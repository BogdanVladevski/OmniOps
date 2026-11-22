using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Domain.Accounts;
using OmniOps.Domain.Billing;
using OmniOps.Domain.Common;
using OmniOps.Domain.Fleet;
using OmniOps.Domain.Jobs;

namespace OmniOps.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext? tenant = null) : DbContext(options), IAppDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<DriverPairingCode> DriverPairingCodes => Set<DriverPairingCode>();
    public DbSet<AccountLink> AccountLinks => Set<AccountLink>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobDocument> JobDocuments => Set<JobDocument>();
    public DbSet<JobLocation> JobLocations => Set<JobLocation>();
    public DbSet<BillingProfile> BillingProfiles => Set<BillingProfile>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceReminder> InvoiceReminders => Set<InvoiceReminder>();

    /// <summary>Read by the tenant query filter on every query; null (anonymous) matches nothing.</summary>
    private Guid? CurrentOrganizationId => tenant?.CurrentOrganizationId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Every company-owned table only ever shows the signed-in company's rows.
        OwnedByCompany<UserAccount>(e => e.OrganizationId);
        OwnedByCompany<DriverPairingCode>(e => e.OrganizationId);
        OwnedByCompany<AccountLink>(e => e.OrganizationId);
        OwnedByCompany<AuditEntry>(e => e.OrganizationId);
        OwnedByCompany<Vehicle>(e => e.OrganizationId);
        OwnedByCompany<Driver>(e => e.OrganizationId);
        OwnedByCompany<Job>(e => e.OrganizationId);
        OwnedByCompany<JobDocument>(e => e.OrganizationId);
        OwnedByCompany<JobLocation>(e => e.OrganizationId);
        OwnedByCompany<BillingProfile>(e => e.OrganizationId);
        OwnedByCompany<Customer>(e => e.OrganizationId);
        OwnedByCompany<Invoice>(e => e.OrganizationId);

        void OwnedByCompany<T>(Expression<Func<T, Guid>> organizationId) where T : class
        {
            var row = organizationId.Parameters[0];
            var matches = Expression.Equal(
                Expression.Convert(organizationId.Body, typeof(Guid?)),
                Expression.Property(Expression.Constant(this), nameof(CurrentOrganizationId)));
            modelBuilder.Entity<T>().HasQueryFilter(IAppDbContext.TenantFilter, Expression.Lambda<Func<T, bool>>(matches, row));
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored as integers by default; strings get an explicit length everywhere they matter.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
