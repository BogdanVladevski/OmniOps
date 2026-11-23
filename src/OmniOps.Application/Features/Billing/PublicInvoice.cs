using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Jobs;
using OmniOps.Domain.Common;

namespace OmniOps.Application.Features.Billing;

/// <summary>Anonymous: the customer's invoice page. The token is the credential; null for unknown tokens.</summary>
[Anonymous]
public sealed record GetPublicInvoiceQuery(string Token) : IQuery<PublicInvoiceDto?>;

public sealed class GetPublicInvoiceHandler(IAppDbContext db, TimeProvider clock) : IQueryHandler<GetPublicInvoiceQuery, PublicInvoiceDto?>
{
    public async ValueTask<PublicInvoiceDto?> Handle(GetPublicInvoiceQuery query, CancellationToken cancellationToken)
    {
        if (!PublicToken.LooksValid(query.Token))
        {
            return null;
        }

        var invoice = await db.Invoices.AcrossCompanies()
            .Include(i => i.Lines)
            .Include(i => i.Job).ThenInclude(j => j!.Documents)
            .FirstOrDefaultAsync(i => i.PublicToken == query.Token, cancellationToken);
        return invoice is null ? null : PublicInvoiceDto.From(invoice, clock.GetUtcNow().UtcDateTime);
    }
}

/// <summary>Anonymous: a photo from the invoiced delivery, reachable only through that invoice's token.</summary>
[Anonymous]
public sealed record GetPublicInvoiceDocumentQuery(string Token, Guid DocumentId) : IQuery<FileContent?>;

public sealed class GetPublicInvoiceDocumentHandler(IAppDbContext db, IFileStore files) : IQueryHandler<GetPublicInvoiceDocumentQuery, FileContent?>
{
    public async ValueTask<FileContent?> Handle(GetPublicInvoiceDocumentQuery query, CancellationToken cancellationToken)
    {
        if (!PublicToken.LooksValid(query.Token))
        {
            return null;
        }

        var document = await db.Invoices.AcrossCompanies()
            .Where(i => i.PublicToken == query.Token && i.JobId != null)
            .SelectMany(i => i.Job!.Documents)
            .IgnoreQueryFilters([IAppDbContext.TenantFilter])
            .FirstOrDefaultAsync(d => d.Id == query.DocumentId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var stream = await files.OpenReadAsync(document.StorageKey, cancellationToken);
        return stream is null ? null : new FileContent(stream, document.ContentType);
    }
}
