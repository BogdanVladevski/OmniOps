using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;

namespace OmniOps.Infrastructure.Persistence;

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork, IInvoiceNumbering
{
    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, ValueTask<T>> work, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            return await work(cancellationToken);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await work(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Takes the next number and advances the counter in one locking statement, so two invoices issued at the same
    /// moment never share a number. Rolled back with the invoice if issuing fails, so numbers have no gaps.
    /// </summary>
    public async Task<int> NextAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var taken = await db.Database
            .SqlQuery<int>($"""
                UPDATE "BillingProfiles" SET "NextInvoiceSequence" = "NextInvoiceSequence" + 1
                WHERE "OrganizationId" = {organizationId}
                RETURNING "NextInvoiceSequence" - 1 AS "Value"
                """)
            .ToListAsync(cancellationToken);
        return taken.Count == 1 ? taken[0] : throw new InvalidOperationException($"No billing profile for organization {organizationId}.");
    }
}
