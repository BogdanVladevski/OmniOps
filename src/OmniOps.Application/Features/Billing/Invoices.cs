using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Customers;
using OmniOps.Application.Features.Jobs;
using OmniOps.Domain.Billing;
using OmniOps.Domain.Common;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Billing;

public sealed record InvoiceLineInput(string Description, decimal Quantity, decimal UnitPrice, decimal VatRatePercent);

/// <summary>
/// Issues an invoice for a delivered job, in one transaction with the number allocation, so a failure gives the
/// number back. Sending is a separate step that runs after commit.
/// </summary>
[RequiresPolicy(Policies.Office)]
public sealed record CreateInvoiceCommand(
    Guid JobId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerAddress,
    string? CustomerTaxId,
    int? PaymentTermsDays,
    IReadOnlyList<InvoiceLineInput> Lines,
    string? Notes) : ICommand<InvoiceDetailDto>, ITransactional
{
    public const int MaxLines = 20;

}

public sealed class CreateInvoiceValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CustomerEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail))
            .WithMessage("That email address does not look right.").MaximumLength(200);
        RuleFor(x => x.CustomerAddress).MaximumLength(500);
        RuleFor(x => x.CustomerTaxId).MaximumLength(50);
        RuleFor(x => x.PaymentTermsDays).InclusiveBetween(0, 180).When(x => x.PaymentTermsDays.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotNull().Must(l => l.Count is > 0 and <= CreateInvoiceCommand.MaxLines)
            .WithMessage($"An invoice needs between 1 and {CreateInvoiceCommand.MaxLines} lines.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description).NotEmpty().MaximumLength(300);
            line.RuleFor(l => l.Quantity).GreaterThan(0).LessThanOrEqualTo(100_000);
            line.RuleFor(l => l.UnitPrice).GreaterThan(0).WithMessage("Enter the price.").LessThanOrEqualTo(10_000_000);
            line.RuleFor(l => l.VatRatePercent).InclusiveBetween(0, 50);
        });
    }
}

public sealed class CreateInvoiceHandler(IAppDbContext db, ITenantContext tenant, IInvoiceNumbering numbering, IAuditLog audit, TimeProvider clock)
    : ICommandHandler<CreateInvoiceCommand, InvoiceDetailDto>
{
    public async ValueTask<InvoiceDetailDto> Handle(CreateInvoiceCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var profile = await db.BillingProfiles.FirstOrDefaultAsync(cancellationToken);
        if (!BillingProfileDto.Complete(profile))
        {
            throw new BusinessRuleException("Add your company name, address and bank account under Invoice settings first.");
        }

        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == command.JobId, cancellationToken)
            ?? throw new KeyNotFoundException("Job not found.");
        if (job.Status != JobStatus.Delivered)
        {
            throw new BusinessRuleException("Only delivered jobs can be invoiced.");
        }

        if (job.InvoiceId is not null)
        {
            throw new BusinessRuleException("This delivery already has an invoice.");
        }

        var terms = command.PaymentTermsDays ?? profile!.DefaultPaymentTermsDays;
        var sequence = await numbering.NextAsync(tenant.OrganizationId, cancellationToken);
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            OrganizationId = tenant.OrganizationId,
            JobId = job.Id,
            Number = InvoiceRules.FormatNumber(profile!.InvoicePrefix, now.Year, sequence),
            Status = InvoiceStatus.Issued,
            IssueDateUtc = now,
            DueDateUtc = now.Date.AddDays(terms),
            Currency = profile.Currency,
            SellerName = profile.LegalName,
            SellerAddress = profile.Address,
            SellerTaxId = profile.TaxId,
            SellerIban = profile.Iban,
            SellerBankName = profile.BankName,
            SellerEmail = profile.Email,
            CustomerName = command.CustomerName.Trim(),
            CustomerEmail = BillingText.Clean(command.CustomerEmail),
            CustomerAddress = BillingText.Clean(command.CustomerAddress),
            CustomerTaxId = BillingText.Clean(command.CustomerTaxId),
            Notes = BillingText.Clean(command.Notes),
            PublicToken = PublicToken.Create(),
            CreatedAtUtc = now,
            Lines = command.Lines.Select((l, i) => new InvoiceLine
            {
                Id = Guid.NewGuid(),
                Position = i,
                Description = l.Description.Trim(),
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                VatRatePercent = l.VatRatePercent,
            }).ToList(),
        };
        InvoiceRules.ApplyTotals(invoice);
        db.Invoices.Add(invoice);
        job.InvoiceId = invoice.Id;

        await RememberCustomerAsync(invoice, job.CustomerId, command.PaymentTermsDays, now, cancellationToken);
        audit.Record("Issue", "Invoice", invoice.Id.ToString(), $"{invoice.Number} {invoice.Total} {invoice.Currency} to {invoice.CustomerName}");
        await db.SaveChangesAsync(cancellationToken);
        return InvoiceDetailDto.From(invoice, now);
    }

    /// <summary>
    /// Links the invoice to the delivery's customer (even when the invoice names them differently, e.g. their legal name)
    /// and keeps their billing details so the next invoice for them is pre-filled.
    /// </summary>
    private async Task RememberCustomerAsync(Invoice invoice, Guid? jobCustomerId, int? terms, DateTime now, CancellationToken cancellationToken)
    {
        var customer = jobCustomerId is { } id
            ? await CustomerDirectory.GetAsync(db, id, cancellationToken)
            : await CustomerDirectory.FindOrCreateAsync(db, invoice.OrganizationId, invoice.CustomerName, now, cancellationToken);
        invoice.CustomerId = customer.Id;

        customer.Email = invoice.CustomerEmail ?? customer.Email;
        customer.Address = invoice.CustomerAddress ?? customer.Address;
        customer.TaxId = invoice.CustomerTaxId ?? customer.TaxId;
        customer.PaymentTermsDays = terms ?? customer.PaymentTermsDays;
        customer.UpdatedAtUtc = now;
    }
}

/// <summary>
/// Emails the invoice link (<paramref name="Email"/> = true) or records that the office sent it another way
/// (WhatsApp, their own email). Either way it becomes "Sent" and reminders start.
/// </summary>
[RequiresPolicy(Policies.Office)]
public sealed record SendInvoiceCommand(Guid InvoiceId, bool Email) : ICommand<InvoiceDetailDto>;

public sealed class SendInvoiceHandler(
    IAppDbContext db, IEmailSender email, IPublicLinks links, IAuditLog audit, TimeProvider clock, ILogger<SendInvoiceHandler> logger)
    : ICommandHandler<SendInvoiceCommand, InvoiceDetailDto>
{
    public async ValueTask<InvoiceDetailDto> Handle(SendInvoiceCommand command, CancellationToken cancellationToken)
    {
        var invoice = await db.InvoiceAsync(command.InvoiceId, cancellationToken);
        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw new BusinessRuleException("This invoice is already paid.");
        }

        var emailed = false;
        if (command.Email)
        {
            if (!email.IsConfigured)
            {
                throw new BusinessRuleException("Email sending is not set up. Copy the invoice link and send it yourself.");
            }

            if (string.IsNullOrWhiteSpace(invoice.CustomerEmail))
            {
                throw new BusinessRuleException("This customer has no email address on the invoice.");
            }

            var (subject, body) = BillingText.InvoiceEmail(invoice, links.Invoice(invoice.PublicToken));
            try
            {
                await email.SendAsync(invoice.CustomerEmail, subject, body, cancellationToken);
                emailed = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Sending invoice {Number} failed", invoice.Number);
                throw new BusinessRuleException("The email could not be sent. Check the address, or copy the link and send it yourself.");
            }
        }

        // Recorded after the email went out: a failure here can at worst lead to the email being sent twice.
        var now = clock.GetUtcNow().UtcDateTime;
        invoice.Status = InvoiceStatus.Sent;
        invoice.SentAtUtc ??= now;
        audit.Record(emailed ? "Email" : "MarkSent", "Invoice", invoice.Id.ToString(), emailed ? invoice.CustomerEmail : null);
        await db.SaveChangesAsync(cancellationToken);
        return InvoiceDetailDto.From(invoice, now, emailed);
    }
}

[RequiresPolicy(Policies.Office)]
public sealed record MarkInvoicePaidCommand(Guid InvoiceId, DateTimeOffset? PaidAt) : ICommand<InvoiceDetailDto>;

public sealed class MarkInvoicePaidHandler(IAppDbContext db, IAuditLog audit, TimeProvider clock) : ICommandHandler<MarkInvoicePaidCommand, InvoiceDetailDto>
{
    public async ValueTask<InvoiceDetailDto> Handle(MarkInvoicePaidCommand command, CancellationToken cancellationToken)
    {
        var invoice = await db.InvoiceAsync(command.InvoiceId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (invoice.Status == InvoiceStatus.Paid)
        {
            return InvoiceDetailDto.From(invoice, now);
        }

        var paidAt = command.PaidAt?.UtcDateTime ?? now;
        if (paidAt > now.AddMinutes(5))
        {
            throw new BusinessRuleException("The payment date cannot be in the future.");
        }

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAtUtc = paidAt;
        audit.Record("MarkPaid", "Invoice", invoice.Id.ToString(), paidAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        await db.SaveChangesAsync(cancellationToken);
        return InvoiceDetailDto.From(invoice, now);
    }
}

[RequiresPolicy(Policies.Office)]
public sealed record GetInvoiceQuery(Guid InvoiceId) : IQuery<InvoiceDetailDto>;

public sealed class GetInvoiceHandler(IAppDbContext db, TimeProvider clock) : IQueryHandler<GetInvoiceQuery, InvoiceDetailDto>
{
    public async ValueTask<InvoiceDetailDto> Handle(GetInvoiceQuery query, CancellationToken cancellationToken) =>
        InvoiceDetailDto.From(await db.InvoiceAsync(query.InvoiceId, cancellationToken), clock.GetUtcNow().UtcDateTime);
}

/// <summary>Money at a glance: what is owed, what is overdue, and delivered jobs still to invoice.</summary>
[RequiresPolicy(Policies.Office)]
public sealed record GetInvoiceOverviewQuery : IQuery<InvoiceOverviewDto>;

public sealed class GetInvoiceOverviewHandler(IAppDbContext db, ITenantContext tenant, TimeProvider clock)
    : IQueryHandler<GetInvoiceOverviewQuery, InvoiceOverviewDto>
{
    private static readonly TimeSpan UninvoicedWindow = TimeSpan.FromDays(90);

    public async ValueTask<InvoiceOverviewDto> Handle(GetInvoiceOverviewQuery query, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var profile = await db.BillingProfiles.FirstOrDefaultAsync(cancellationToken);
        var terms = WaitingTerms.From(profile);
        var invoices = await db.Invoices.Include(i => i.Reminders)
            .OrderByDescending(i => i.IssueDateUtc).Take(500).ToListAsync(cancellationToken);

        var since = now - UninvoicedWindow;
        var toInvoice = await db.Visible(tenant)
            .Where(j => j.Status == JobStatus.Delivered && j.InvoiceId == null && j.DeliveredAtUtc >= since)
            .OrderByDescending(j => j.DeliveredAtUtc).Take(200).ToListAsync(cancellationToken);
        var customerIds = toInvoice.Where(j => j.CustomerId != null).Select(j => j.CustomerId!.Value).Distinct().ToList();
        var customers = await db.Customers.Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

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
                var c = j.CustomerId is { } id ? customers.GetValueOrDefault(id) : null;
                return new UninvoicedJobDto(JobDto.From(j), BillingText.Describe(j), c?.Email, c?.Address, c?.TaxId, c?.PaymentTermsDays,
                    terms.Waits(j, now), terms.SuggestedLines(j, now));
            }).ToList());
    }
}

internal static class InvoiceQueries
{
    public static async Task<Invoice> InvoiceAsync(this IAppDbContext db, Guid invoiceId, CancellationToken ct) =>
        await db.Invoices.Include(i => i.Lines).Include(i => i.Reminders).FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
        ?? throw new KeyNotFoundException("Invoice not found.");
}
