using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly AppDbContext _context;

    public InvoiceRepository(AppDbContext context) => _context = context;

    public Task<BillingProfile?> GetProfileAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        _context.BillingProfiles.FirstOrDefaultAsync(p => p.OrganizationId == organizationId, cancellationToken);

    public async Task AddProfileAsync(BillingProfile profile, CancellationToken cancellationToken = default) =>
        await _context.BillingProfiles.AddAsync(profile, cancellationToken);

    public async Task<int> AllocateInvoiceSequenceAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var taken = await _context.Database
            .SqlQuery<int>($@"UPDATE ""BillingProfiles"" SET ""NextInvoiceSequence"" = ""NextInvoiceSequence"" + 1
                             WHERE ""OrganizationId"" = {organizationId}
                             RETURNING ""NextInvoiceSequence"" - 1 AS ""Value""")
            .ToListAsync(cancellationToken);
        return taken.Count == 1
            ? taken[0]
            : throw new InvalidOperationException($"No billing profile for organization {organizationId}.");
    }

    public Task<Customer?> FindCustomerAsync(Guid organizationId, string normalizedName, CancellationToken cancellationToken = default) =>
        _context.Customers.FirstOrDefaultAsync(
            c => c.OrganizationId == organizationId && c.NormalizedName == normalizedName, cancellationToken);

    public async Task<IReadOnlyList<Customer>> FindCustomersAsync(
        Guid organizationId, IReadOnlyCollection<string> normalizedNames, CancellationToken cancellationToken = default) =>
        await _context.Customers.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && normalizedNames.Contains(c.NormalizedName))
            .ToListAsync(cancellationToken);

    public async Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default) =>
        await _context.Customers.AddAsync(customer, cancellationToken);

    public Task<Invoice?> GetAsync(Guid invoiceId, Guid organizationId, CancellationToken cancellationToken = default) =>
        _context.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Reminders)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.OrganizationId == organizationId, cancellationToken);

    public async Task<IReadOnlyList<Invoice>> ListAsync(Guid organizationId, int take, CancellationToken cancellationToken = default) =>
        await _context.Invoices.AsNoTracking()
            .Include(i => i.Reminders)
            .Where(i => i.OrganizationId == organizationId)
            .OrderByDescending(i => i.IssueDateUtc).ThenByDescending(i => i.Number)
            .Take(take)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public Task<Invoice?> GetByPublicTokenAsync(string token, CancellationToken cancellationToken = default) =>
        _context.Invoices.AsNoTracking()
            .Include(i => i.Lines)
            .Include(i => i.Job).ThenInclude(j => j!.Documents)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.PublicToken == token, cancellationToken);

    public async Task<IReadOnlyList<Invoice>> ListAwaitingPaymentAsync(int take, CancellationToken cancellationToken = default) =>
        await _context.Invoices
            .Include(i => i.Reminders)
            .Where(i => i.Status == InvoiceStatus.Sent)
            .OrderBy(i => i.DueDateUtc)
            .Take(take)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default) =>
        await _context.Invoices.AddAsync(invoice, cancellationToken);

    public async Task AddReminderAsync(InvoiceReminder reminder, CancellationToken cancellationToken = default) =>
        await _context.InvoiceReminders.AddAsync(reminder, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
