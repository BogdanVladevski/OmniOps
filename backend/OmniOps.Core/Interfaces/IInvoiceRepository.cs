using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

/// <summary>Invoices, billing profile and customers. Organization-scoped methods return nothing for other organizations.</summary>
public interface IInvoiceRepository
{
    Task<BillingProfile?> GetProfileAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task AddProfileAsync(BillingProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the next invoice sequence number. Must run inside the transaction that inserts the invoice:
    /// the row stays locked until commit, and a rollback returns the number.
    /// </summary>
    Task<int> AllocateInvoiceSequenceAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<Customer?> FindCustomerAsync(Guid organizationId, string normalizedName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Customer>> FindCustomersAsync(Guid organizationId, IReadOnlyCollection<string> normalizedNames, CancellationToken cancellationToken = default);
    Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default);

    /// <summary>Tracked invoice with lines and reminders.</summary>
    Task<Invoice?> GetAsync(Guid invoiceId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> ListAsync(Guid organizationId, int take, CancellationToken cancellationToken = default);
    Task<Invoice?> GetByPublicTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Sent, unpaid invoices across all organizations (reminder worker).</summary>
    Task<IReadOnlyList<Invoice>> ListAwaitingPaymentAsync(int take, CancellationToken cancellationToken = default);

    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task AddReminderAsync(InvoiceReminder reminder, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
