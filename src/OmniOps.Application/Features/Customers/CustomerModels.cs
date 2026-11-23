using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Features.Billing;
using OmniOps.Application.Features.Jobs;
using OmniOps.Domain.Billing;

namespace OmniOps.Application.Features.Customers;

/// <summary>A customer as the office sees it, with where things stand.</summary>
public sealed record CustomerDto(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxId,
    int? PaymentTermsDays,
    bool EmailUpdates,
    string? PortalUrl,
    DateTime? PortalCreatedAtUtc,
    int ActiveJobs,
    int DeliveredLast30Days,
    DateTime? LastJobAtUtc,
    decimal Outstanding,
    decimal Overdue,
    string Currency);

public sealed record CustomerDetailDto(CustomerDto Customer, IReadOnlyList<JobDto> RecentJobs, IReadOnlyList<InvoiceSummaryDto> Invoices);

internal static class CustomerDirectory
{
    /// <summary>
    /// The signed-in company's customer with this name (case and spacing don't matter), created if new. Not saved:
    /// the caller's SaveChanges commits it together with the job or invoice that needs it.
    /// </summary>
    public static async Task<Customer> FindOrCreateAsync(IAppDbContext db, Guid organizationId, string name, DateTime nowUtc, CancellationToken ct)
    {
        var normalized = BillingText.NormalizeName(name);
        var customer = db.Customers.Local.FirstOrDefault(c => c.OrganizationId == organizationId && c.NormalizedName == normalized)
            ?? await db.Customers.FirstOrDefaultAsync(c => c.NormalizedName == normalized, ct);
        if (customer is not null)
        {
            return customer;
        }

        customer = new Customer
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name.Trim(),
            NormalizedName = normalized,
            UpdatedAtUtc = nowUtc,
        };
        db.Customers.Add(customer);
        return customer;
    }

    public static async Task<Customer> GetAsync(IAppDbContext db, Guid customerId, CancellationToken ct) =>
        await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct) ?? throw new KeyNotFoundException("Customer not found.");

    /// <summary>Customers with their numbers. Runs under the tenant filter: only the signed-in company's rows.</summary>
    public static async Task<List<CustomerDto>> SummariesAsync(
        IAppDbContext db, IPublicLinks links, IQueryable<Customer> customers, DateTime nowUtc, CancellationToken ct)
    {
        var list = await customers.OrderBy(c => c.Name).ToListAsync(ct);
        var ids = list.Select(c => c.Id).ToList();
        var since = nowUtc.AddDays(-30);

        var jobs = await db.Jobs.Where(j => j.CustomerId != null && ids.Contains(j.CustomerId.Value))
            .GroupBy(j => j.CustomerId!.Value)
            .Select(g => new
            {
                Id = g.Key,
                Active = g.Count(j => j.Status == Domain.Jobs.JobStatus.Planned || j.Status == Domain.Jobs.JobStatus.InProgress),
                Delivered = g.Count(j => j.Status == Domain.Jobs.JobStatus.Delivered && j.DeliveredAtUtc >= since),
                Last = g.Max(j => (DateTime?)j.CreatedAtUtc),
            })
            .ToDictionaryAsync(x => x.Id, ct);

        var unpaid = await db.Invoices.Where(i => i.CustomerId != null && ids.Contains(i.CustomerId.Value) && i.Status != InvoiceStatus.Paid)
            .Select(i => new { CustomerId = i.CustomerId!.Value, i.Total, i.DueDateUtc, i.Currency, i.Status })
            .ToListAsync(ct);
        var currency = await db.BillingProfiles.Select(p => p.Currency).FirstOrDefaultAsync(ct) ?? "EUR";

        return list.Select(c =>
        {
            jobs.TryGetValue(c.Id, out var j);
            var owed = unpaid.Where(i => i.CustomerId == c.Id).ToList();
            return new CustomerDto(
                c.Id, c.Name, c.Email, c.Phone, c.Address, c.TaxId, c.PaymentTermsDays, c.EmailUpdates,
                c.PortalToken is null ? null : links.Portal(c.PortalToken), c.PortalCreatedAtUtc,
                j?.Active ?? 0, j?.Delivered ?? 0, j?.Last,
                owed.Sum(i => i.Total),
                owed.Where(i => i.Status == InvoiceStatus.Sent && i.DueDateUtc.Date < nowUtc.Date).Sum(i => i.Total),
                currency);
        }).ToList();
    }
}
