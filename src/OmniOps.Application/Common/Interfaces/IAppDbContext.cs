using Microsoft.EntityFrameworkCore;
using OmniOps.Domain.Accounts;
using OmniOps.Domain.Billing;
using OmniOps.Domain.Common;
using OmniOps.Domain.Fleet;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Common.Interfaces;

/// <summary>
/// The database, as handlers see it. Company-owned sets are filtered to the signed-in company automatically
/// (query filter <see cref="TenantFilter"/>); code that must look across companies — public links, background
/// work — says so with <c>IgnoreQueryFilters([TenantFilter])</c>.
/// </summary>
public interface IAppDbContext
{
    public const string TenantFilter = "Tenant";

    DbSet<Organization> Organizations { get; }
    DbSet<UserAccount> Users { get; }
    DbSet<DriverPairingCode> DriverPairingCodes { get; }
    DbSet<AccountLink> AccountLinks { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditEntry> AuditEntries { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<Driver> Drivers { get; }
    DbSet<Job> Jobs { get; }
    DbSet<JobDocument> JobDocuments { get; }
    DbSet<JobLocation> JobLocations { get; }
    DbSet<BillingProfile> BillingProfiles { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceReminder> InvoiceReminders { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
