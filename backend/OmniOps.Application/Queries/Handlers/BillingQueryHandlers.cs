using MediatR;
using OmniOps.Application.Commands.Handlers;
using OmniOps.Application.Dtos;
using OmniOps.Core.Domain;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetBillingProfileQueryHandler : IRequestHandler<GetBillingProfileQuery, BillingProfileDto>
{
    private readonly IInvoiceRepository _invoices;
    private readonly ITenantContext _tenant;
    private readonly IEmailSender _email;

    public GetBillingProfileQueryHandler(IInvoiceRepository invoices, ITenantContext tenant, IEmailSender email)
    {
        _invoices = invoices;
        _tenant = tenant;
        _email = email;
    }

    public async Task<BillingProfileDto> Handle(GetBillingProfileQuery request, CancellationToken cancellationToken) =>
        BillingProfileDto.From(await _invoices.GetProfileAsync(_tenant.OrganizationId, cancellationToken), _email.IsConfigured);
}

public class GetInvoiceOverviewQueryHandler : IRequestHandler<GetInvoiceOverviewQuery, InvoiceOverviewDto>
{
    private static readonly TimeSpan UninvoicedWindow = TimeSpan.FromDays(90);
    private readonly IInvoiceRepository _invoices;
    private readonly IJobRepository _jobs;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;

    public GetInvoiceOverviewQueryHandler(IInvoiceRepository invoices, IJobRepository jobs, ITenantContext tenant, TimeProvider clock)
    {
        _invoices = invoices;
        _jobs = jobs;
        _tenant = tenant;
        _clock = clock;
    }

    public async Task<InvoiceOverviewDto> Handle(GetInvoiceOverviewQuery request, CancellationToken cancellationToken)
    {
        var org = _tenant.OrganizationId;
        var now = _clock.GetUtcNow().UtcDateTime;
        var profile = await _invoices.GetProfileAsync(org, cancellationToken);
        var invoices = await _invoices.ListAsync(org, 500, cancellationToken);

        var delivered = await _jobs.ListAsync(org, [TripStatus.Completed], null, now - UninvoicedWindow, 200, cancellationToken);
        var toInvoice = delivered.Where(j => j.InvoiceId is null).ToList();
        var customers = (await _invoices.FindCustomersAsync(
                org, toInvoice.Select(j => BillingText.NormalizeName(j.CustomerName ?? string.Empty)).Distinct().ToList(), cancellationToken))
            .ToDictionary(c => c.NormalizedName);

        var unpaid = invoices.Where(i => i.Status != InvoiceStatus.Paid).ToList();
        var overdue = unpaid.Where(i => InvoiceRules.IsOverdue(i, now)).ToList();

        return new InvoiceOverviewDto(
            profile?.Currency ?? "EUR",
            unpaid.Sum(i => i.Total),
            overdue.Sum(i => i.Total),
            overdue.Count,
            invoices.Where(i => i.Status == InvoiceStatus.Paid && i.PaidAtUtc >= now.AddDays(-30)).Sum(i => i.Total),
            invoices.Select(i => InvoiceSummaryDto.From(i, now)).ToList(),
            toInvoice.Select(j =>
            {
                customers.TryGetValue(BillingText.NormalizeName(j.CustomerName ?? string.Empty), out var c);
                return new UninvoicedJobDto(JobDto.FromEntity(j), Describe(j), c?.Email, c?.Address, c?.TaxId, c?.PaymentTermsDays);
            }).ToList());
    }

    /// <summary>"Transport Skopje depot → Bitola, order PO-2231, delivered 22 Nov 2026".</summary>
    internal static string Describe(Core.Entities.Trip j)
    {
        var route = string.Join(" → ", new[] { j.Origin, j.Destination }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var parts = new List<string> { string.IsNullOrEmpty(route) ? "Transport" : $"Transport {route}" };
        if (!string.IsNullOrWhiteSpace(j.Reference)) parts.Add($"order {j.Reference}");
        if (j.CompletedAtUtc is { } at) parts.Add($"delivered {BillingText.Day(at)}");
        return string.Join(", ", parts);
    }
}

public class GetInvoiceQueryHandler : IRequestHandler<GetInvoiceQuery, InvoiceDetailDto>
{
    private readonly IInvoiceRepository _invoices;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;

    public GetInvoiceQueryHandler(IInvoiceRepository invoices, ITenantContext tenant, TimeProvider clock)
    {
        _invoices = invoices;
        _tenant = tenant;
        _clock = clock;
    }

    public async Task<InvoiceDetailDto> Handle(GetInvoiceQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _invoices.GetAsync(request.InvoiceId, _tenant.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice {request.InvoiceId} not found.");
        return InvoiceDetailDto.From(invoice, _clock.GetUtcNow().UtcDateTime);
    }
}

public class GetPublicInvoiceQueryHandler : IRequestHandler<GetPublicInvoiceQuery, PublicInvoiceDto?>
{
    private readonly IInvoiceRepository _invoices;
    private readonly TimeProvider _clock;

    public GetPublicInvoiceQueryHandler(IInvoiceRepository invoices, TimeProvider clock)
    {
        _invoices = invoices;
        _clock = clock;
    }

    public async Task<PublicInvoiceDto?> Handle(GetPublicInvoiceQuery request, CancellationToken cancellationToken)
    {
        if (!TrackingToken.LooksValid(request.Token))
        {
            return null;
        }

        var invoice = await _invoices.GetByPublicTokenAsync(request.Token, cancellationToken);
        return invoice is null ? null : PublicInvoiceDto.From(invoice, _clock.GetUtcNow().UtcDateTime);
    }
}

public class GetPublicInvoiceDocumentQueryHandler : IRequestHandler<GetPublicInvoiceDocumentQuery, JobDocumentContent?>
{
    private readonly IInvoiceRepository _invoices;
    private readonly IFileStore _files;

    public GetPublicInvoiceDocumentQueryHandler(IInvoiceRepository invoices, IFileStore files)
    {
        _invoices = invoices;
        _files = files;
    }

    public async Task<JobDocumentContent?> Handle(GetPublicInvoiceDocumentQuery request, CancellationToken cancellationToken)
    {
        if (!TrackingToken.LooksValid(request.Token))
        {
            return null;
        }

        // Only documents of the invoiced job are reachable through the invoice's token.
        var invoice = await _invoices.GetByPublicTokenAsync(request.Token, cancellationToken);
        var document = invoice?.Job?.Documents.FirstOrDefault(d => d.Id == request.DocumentId);
        if (document is null)
        {
            return null;
        }

        var stream = await _files.OpenReadAsync(document.StorageKey, cancellationToken);
        return stream is null ? null : new JobDocumentContent(stream, document.ContentType);
    }
}
