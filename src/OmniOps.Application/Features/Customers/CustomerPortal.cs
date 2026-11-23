using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Jobs;
using OmniOps.Domain.Billing;
using OmniOps.Domain.Common;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Customers;

// The customer's portal: one link that shows the customer everything the carrier is doing for them. It shows only
// this customer's jobs and invoices, and nothing about drivers or other customers. The link token is the credential.

public sealed record CustomerPortalDto(
    string CompanyName,
    string CustomerName,
    IReadOnlyList<PortalJobDto> OnTheWay,
    IReadOnlyList<PortalJobDto> Planned,
    IReadOnlyList<PortalJobDto> Delivered,
    IReadOnlyList<PortalInvoiceDto> Invoices);

public sealed record PortalJobDto(
    Guid Id,
    string? Reference,
    string Status,
    string? Origin,
    string? Destination,
    DateTime? PlannedDeliveryUtc,
    DateTime? StartedAtUtc,
    DateTime? EtaUtc,
    double? RemainingKm,
    DateTime? ArrivedAtUtc,
    DateTime? DeliveredAtUtc,
    string? ReceivedBy,
    int PhotoCount,
    bool HasSignature,
    int TemperatureReadings,
    int ProblemsReported,
    string? TrackingPath,
    string? ProofPath);

public sealed record PortalInvoiceDto(
    string Number,
    DateTime IssueDateUtc,
    DateTime DueDateUtc,
    decimal Total,
    string Currency,
    string Status,
    string? DeliveryReference,
    string PublicPath);

[Anonymous]
public sealed record GetCustomerPortalQuery(string Token) : IQuery<CustomerPortalDto?>;

public sealed class GetCustomerPortalHandler(IAppDbContext db, TimeProvider clock) : IQueryHandler<GetCustomerPortalQuery, CustomerPortalDto?>
{
    /// <summary>Deliveries shown on the portal; older ones remain in the carrier's records.</summary>
    public static readonly TimeSpan DeliveredWindow = TimeSpan.FromDays(90);

    public async ValueTask<CustomerPortalDto?> Handle(GetCustomerPortalQuery query, CancellationToken cancellationToken)
    {
        if (await PortalAccess.CustomerAsync(db, query.Token, cancellationToken) is not { } customer)
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var since = now - DeliveredWindow;
        var company = await db.Organizations.Where(o => o.Id == customer.OrganizationId).Select(o => o.Name).FirstAsync(cancellationToken);
        var jobs = await db.Jobs.AcrossCompanies()
            .Include(j => j.Documents)
            .Where(j => j.OrganizationId == customer.OrganizationId && j.CustomerId == customer.Id && j.Status != JobStatus.Cancelled)
            .Where(j => j.Status != JobStatus.Delivered || j.DeliveredAtUtc >= since)
            .OrderByDescending(j => j.DeliveredAtUtc ?? j.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        var referenceByInvoice = jobs.Where(j => j.InvoiceId != null).ToDictionary(j => j.InvoiceId!.Value, j => j.Reference);

        var invoices = await db.Invoices.AcrossCompanies()
            .Where(i => i.OrganizationId == customer.OrganizationId && i.CustomerId == customer.Id)
            .Where(i => i.Status != InvoiceStatus.Paid || i.PaidAtUtc >= now.AddYears(-1))
            .OrderByDescending(i => i.IssueDateUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        PortalJobDto ToDto(Job j)
        {
            (double? km, DateTime? eta) estimate = (null, null);
            if (j is { Status: JobStatus.InProgress, LastLatitude: { } lat, LastLongitude: { } lon, LastPositionAtUtc: { } at,
                DestinationLatitude: { } toLat, DestinationLongitude: { } toLon })
            {
                estimate = DeliveryEta.Estimate(lat, lon, toLat, toLon, at);
            }

            var delivered = j.Status == JobStatus.Delivered;
            return new PortalJobDto(
                j.Id, j.Reference,
                j.Status switch { JobStatus.InProgress => "OnTheWay", JobStatus.Delivered => "Delivered", _ => "Planned" },
                j.Origin, j.Destination, j.PlannedDeliveryUtc, j.StartedAtUtc, estimate.eta, estimate.km,
                j.DeliveryArrivedAtUtc, j.DeliveredAtUtc, delivered ? j.RecipientName : null,
                j.Documents.Count(d => d.Kind is JobDocumentKind.DeliveryPhoto or JobDocumentKind.PickupDocument),
                j.Documents.Any(d => d.Kind == JobDocumentKind.Signature),
                j.Documents.Count(d => d.Kind == JobDocumentKind.TemperaturePhoto),
                j.Documents.Count(d => d.Kind == JobDocumentKind.Exception),
                !delivered && j.TrackingToken is { } t ? $"/t/{t}" : null,
                delivered ? $"/api/public/portal/{query.Token}/jobs/{j.Id}/proof.pdf" : null);
        }

        return new CustomerPortalDto(
            company,
            customer.Name,
            jobs.Where(j => j.Status == JobStatus.InProgress).OrderBy(j => j.StartedAtUtc).Select(ToDto).ToList(),
            jobs.Where(j => j.Status == JobStatus.Planned).OrderBy(j => j.PlannedDeliveryUtc ?? j.CreatedAtUtc).Select(ToDto).ToList(),
            jobs.Where(j => j.Status == JobStatus.Delivered).Select(ToDto).ToList(),
            invoices.Select(i => new PortalInvoiceDto(
                i.Number, i.IssueDateUtc, i.DueDateUtc, i.Total, i.Currency,
                i.Status == InvoiceStatus.Paid ? "Paid" : InvoiceRules.IsOverdue(i, now) ? "Overdue" : "Unpaid",
                i.JobId is { } jobId ? referenceByInvoice.GetValueOrDefault(jobId) : null,
                $"/i/{i.PublicToken}")).ToList());
    }
}

/// <summary>The proof pack of one of the portal's delivered jobs.</summary>
[Anonymous]
public sealed record GetPortalProofPackQuery(string Token, Guid JobId) : IQuery<PdfFile?>;

public sealed class GetPortalProofPackHandler(IAppDbContext db, IFileStore files, IProofPackRenderer renderer, TimeProvider clock)
    : IQueryHandler<GetPortalProofPackQuery, PdfFile?>
{
    public async ValueTask<PdfFile?> Handle(GetPortalProofPackQuery query, CancellationToken cancellationToken)
    {
        if (await PortalAccess.CustomerAsync(db, query.Token, cancellationToken) is not { } customer)
        {
            return null;
        }

        var job = await db.Jobs.AcrossCompanies()
            .Include(j => j.Vehicle)
            .Include(j => j.Documents)
            .FirstOrDefaultAsync(j => j.Id == query.JobId && j.OrganizationId == customer.OrganizationId
                && j.CustomerId == customer.Id && j.Status == JobStatus.Delivered, cancellationToken);
        return job is null ? null : await ProofPacks.RenderAsync(db, files, renderer, job, clock.GetUtcNow().UtcDateTime, cancellationToken);
    }
}

internal static class PortalAccess
{
    /// <summary>The customer whose portal link this is, or null (malformed, unknown or turned-off link).</summary>
    public static async Task<Domain.Billing.Customer?> CustomerAsync(IAppDbContext db, string token, CancellationToken ct) =>
        PublicToken.LooksValid(token)
            ? await db.Customers.AcrossCompanies().FirstOrDefaultAsync(c => c.PortalToken == token, ct)
            : null;
}
