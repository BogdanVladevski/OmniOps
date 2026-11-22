using System.Globalization;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Dtos;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Exceptions;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

internal static class BillingText
{
    public static string Money(decimal amount, string currency) =>
        $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {currency}";

    public static string Day(DateTime utc) => utc.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string NormalizeName(string name) =>
        string.Join(' ', name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    public static string PaymentLine(Invoice i) =>
        string.IsNullOrWhiteSpace(i.SellerIban)
            ? string.Empty
            : $"Pay to {i.SellerIban}{(string.IsNullOrWhiteSpace(i.SellerBankName) ? "" : $" ({i.SellerBankName})")}, reference {i.Number}.\n\n";

    public static (string Subject, string Body) InvoiceEmail(Invoice i, string link) => (
        $"Invoice {i.Number} from {i.SellerName}",
        $"Hello {i.CustomerName},\n\n" +
        $"Please find invoice {i.Number} for {Money(i.Total, i.Currency)}, due on {Day(i.DueDateUtc)}.\n\n" +
        $"View or print the invoice, with the proof of delivery: {link}\n\n" +
        PaymentLine(i) +
        $"Thank you,\n{i.SellerName}");

    public static (string Subject, string Body) ReminderEmail(Invoice i, ReminderKind kind, string link)
    {
        var amount = Money(i.Total, i.Currency);
        var (subject, opening) = kind switch
        {
            ReminderKind.DueSoon => ($"Reminder: invoice {i.Number} is due on {Day(i.DueDateUtc)}",
                $"A friendly reminder that invoice {i.Number} for {amount} is due on {Day(i.DueDateUtc)}."),
            ReminderKind.DueToday => ($"Invoice {i.Number} is due today",
                $"Invoice {i.Number} for {amount} is due today."),
            ReminderKind.Overdue7 => ($"Overdue: invoice {i.Number}",
                $"Invoice {i.Number} for {amount} was due on {Day(i.DueDateUtc)} and is now a week overdue."),
            _ => ($"Second notice: invoice {i.Number} is overdue",
                $"Invoice {i.Number} for {amount} was due on {Day(i.DueDateUtc)} and is now two weeks overdue. " +
                "Please arrange payment or let us know if there is a problem with it."),
        };
        return (subject,
            $"Hello {i.CustomerName},\n\n{opening}\n\n" +
            $"Invoice and proof of delivery: {link}\n\n" +
            PaymentLine(i) +
            "If you have already paid, thank you, and please ignore this message.\n\n" +
            $"Kind regards,\n{i.SellerName}");
    }
}

public class SaveBillingProfileCommandHandler : IRequestHandler<SaveBillingProfileCommand, BillingProfileDto>
{
    private readonly IInvoiceRepository _invoices;
    private readonly ITenantContext _tenant;
    private readonly IEmailSender _email;
    private readonly IAuditService _audit;
    private readonly TimeProvider _clock;

    public SaveBillingProfileCommandHandler(IInvoiceRepository invoices, ITenantContext tenant, IEmailSender email, IAuditService audit, TimeProvider clock)
    {
        _invoices = invoices;
        _tenant = tenant;
        _email = email;
        _audit = audit;
        _clock = clock;
    }

    public async Task<BillingProfileDto> Handle(SaveBillingProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await _invoices.GetProfileAsync(_tenant.OrganizationId, cancellationToken);
        if (profile is null)
        {
            profile = new BillingProfile { OrganizationId = _tenant.OrganizationId };
            await _invoices.AddProfileAsync(profile, cancellationToken);
        }

        // NextInvoiceSequence is deliberately never touched here: only issuing an invoice advances it.
        profile.LegalName = request.LegalName.Trim();
        profile.Address = request.Address.Trim();
        profile.TaxId = Clean(request.TaxId);
        profile.Iban = Clean(request.Iban)?.Replace(" ", string.Empty).ToUpperInvariant();
        profile.BankName = Clean(request.BankName);
        profile.Email = Clean(request.Email);
        profile.Phone = Clean(request.Phone);
        profile.Currency = request.Currency.ToUpperInvariant();
        profile.DefaultVatRatePercent = request.DefaultVatRatePercent;
        profile.DefaultPaymentTermsDays = request.DefaultPaymentTermsDays;
        profile.InvoicePrefix = request.InvoicePrefix.Trim().ToUpperInvariant();
        profile.UpdatedAtUtc = _clock.GetUtcNow().UtcDateTime;

        await _audit.LogAsync("Update", "BillingProfile", profile.OrganizationId.ToString(), _tenant.UserId, null, cancellationToken);
        return BillingProfileDto.From(profile, _email.IsConfigured);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, InvoiceDetailDto>
{
    private readonly IInvoiceRepository _invoices;
    private readonly IJobRepository _jobs;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;
    private readonly TimeProvider _clock;

    public CreateInvoiceCommandHandler(IInvoiceRepository invoices, IJobRepository jobs, ITenantContext tenant, IAuditService audit, TimeProvider clock)
    {
        _invoices = invoices;
        _jobs = jobs;
        _tenant = tenant;
        _audit = audit;
        _clock = clock;
    }

    public async Task<InvoiceDetailDto> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _tenant.OrganizationId;
        var now = _clock.GetUtcNow().UtcDateTime;

        var profile = await _invoices.GetProfileAsync(organizationId, cancellationToken);
        if (!BillingProfileDto.Complete(profile))
        {
            throw new BusinessRuleException("Add your company name, address and bank account under Invoice settings first.");
        }

        var job = await _jobs.GetAsync(request.JobId, organizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Job {request.JobId} not found.");
        if (job.Status != TripStatus.Completed)
        {
            throw new BusinessRuleException("Only delivered jobs can be invoiced.");
        }

        if (job.InvoiceId is not null)
        {
            throw new BusinessRuleException("This delivery already has an invoice.");
        }

        var terms = request.PaymentTermsDays ?? profile!.DefaultPaymentTermsDays;
        var sequence = await _invoices.AllocateInvoiceSequenceAsync(organizationId, cancellationToken);
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
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
            CustomerName = request.CustomerName.Trim(),
            CustomerEmail = Clean(request.CustomerEmail),
            CustomerAddress = Clean(request.CustomerAddress),
            CustomerTaxId = Clean(request.CustomerTaxId),
            Notes = Clean(request.Notes),
            PublicToken = TrackingToken.Create(),
            CreatedAtUtc = now,
            Lines = request.Lines.Select((l, i) => new InvoiceLine
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
        await _invoices.AddAsync(invoice, cancellationToken);
        job.InvoiceId = invoice.Id;

        await RememberCustomerAsync(invoice, request.PaymentTermsDays, now, cancellationToken);
        await _audit.LogAsync("Issue", "Invoice", invoice.Id.ToString(), _tenant.UserId,
            $"{invoice.Number} {invoice.Total} {invoice.Currency} to {invoice.CustomerName}", cancellationToken);
        return InvoiceDetailDto.From(invoice, now);
    }

    /// <summary>Keeps the customer's billing details so the next invoice for them is pre-filled.</summary>
    private async Task RememberCustomerAsync(Invoice invoice, int? terms, DateTime now, CancellationToken cancellationToken)
    {
        var normalized = BillingText.NormalizeName(invoice.CustomerName);
        var customer = await _invoices.FindCustomerAsync(invoice.OrganizationId, normalized, cancellationToken);
        if (customer is null)
        {
            customer = new Customer { Id = Guid.NewGuid(), OrganizationId = invoice.OrganizationId, NormalizedName = normalized };
            await _invoices.AddCustomerAsync(customer, cancellationToken);
        }

        customer.Name = invoice.CustomerName;
        customer.Email = invoice.CustomerEmail ?? customer.Email;
        customer.Address = invoice.CustomerAddress ?? customer.Address;
        customer.TaxId = invoice.CustomerTaxId ?? customer.TaxId;
        customer.PaymentTermsDays = terms ?? customer.PaymentTermsDays;
        customer.UpdatedAtUtc = now;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public class SendInvoiceCommandHandler : IRequestHandler<SendInvoiceCommand, InvoiceDetailDto>
{
    private readonly IInvoiceRepository _invoices;
    private readonly ITenantContext _tenant;
    private readonly IEmailSender _email;
    private readonly IPublicLinks _links;
    private readonly IAuditService _audit;
    private readonly TimeProvider _clock;
    private readonly ILogger<SendInvoiceCommandHandler> _logger;

    public SendInvoiceCommandHandler(
        IInvoiceRepository invoices, ITenantContext tenant, IEmailSender email, IPublicLinks links,
        IAuditService audit, TimeProvider clock, ILogger<SendInvoiceCommandHandler> logger)
    {
        _invoices = invoices;
        _tenant = tenant;
        _email = email;
        _links = links;
        _audit = audit;
        _clock = clock;
        _logger = logger;
    }

    public async Task<InvoiceDetailDto> Handle(SendInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _invoices.GetAsync(request.InvoiceId, _tenant.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice {request.InvoiceId} not found.");
        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw new BusinessRuleException("This invoice is already paid.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var emailed = false;
        if (request.Email)
        {
            if (!_email.IsConfigured)
            {
                throw new BusinessRuleException("Email sending is not set up. Copy the invoice link and send it yourself.");
            }

            if (string.IsNullOrWhiteSpace(invoice.CustomerEmail))
            {
                throw new BusinessRuleException("This customer has no email address on the invoice.");
            }

            var (subject, body) = BillingText.InvoiceEmail(invoice, _links.Invoice(invoice.PublicToken));
            try
            {
                await _email.SendAsync(invoice.CustomerEmail, subject, body, cancellationToken);
                emailed = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sending invoice {Number} failed", invoice.Number);
                throw new BusinessRuleException("The email could not be sent. Check the address, or copy the link and send it yourself.");
            }
        }

        // Recorded after the email went out: a failure here can at worst lead to the email being sent twice.
        invoice.Status = InvoiceStatus.Sent;
        invoice.SentAtUtc ??= now;
        await _audit.LogAsync(emailed ? "Email" : "MarkSent", "Invoice", invoice.Id.ToString(), _tenant.UserId,
            emailed ? invoice.CustomerEmail : null, cancellationToken);
        return InvoiceDetailDto.From(invoice, now, emailed);
    }
}

public class MarkInvoicePaidCommandHandler : IRequestHandler<MarkInvoicePaidCommand, InvoiceDetailDto>
{
    private readonly IInvoiceRepository _invoices;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;
    private readonly TimeProvider _clock;

    public MarkInvoicePaidCommandHandler(IInvoiceRepository invoices, ITenantContext tenant, IAuditService audit, TimeProvider clock)
    {
        _invoices = invoices;
        _tenant = tenant;
        _audit = audit;
        _clock = clock;
    }

    public async Task<InvoiceDetailDto> Handle(MarkInvoicePaidCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _invoices.GetAsync(request.InvoiceId, _tenant.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice {request.InvoiceId} not found.");
        var now = _clock.GetUtcNow().UtcDateTime;
        if (invoice.Status == InvoiceStatus.Paid)
        {
            return InvoiceDetailDto.From(invoice, now);
        }

        var paidAt = request.PaidAt?.UtcDateTime ?? now;
        if (paidAt > now.AddMinutes(5))
        {
            throw new BusinessRuleException("The payment date cannot be in the future.");
        }

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAtUtc = paidAt;
        await _audit.LogAsync("MarkPaid", "Invoice", invoice.Id.ToString(), _tenant.UserId, paidAt.ToString("O"), cancellationToken);
        return InvoiceDetailDto.From(invoice, now);
    }
}

public class ProcessInvoiceRemindersCommandHandler : IRequestHandler<ProcessInvoiceRemindersCommand, InvoiceReminderRunDto>
{
    private const int BatchSize = 500;
    private readonly IInvoiceRepository _invoices;
    private readonly IEmailSender _email;
    private readonly IPublicLinks _links;
    private readonly TimeProvider _clock;
    private readonly ILogger<ProcessInvoiceRemindersCommandHandler> _logger;

    public ProcessInvoiceRemindersCommandHandler(
        IInvoiceRepository invoices, IEmailSender email, IPublicLinks links, TimeProvider clock,
        ILogger<ProcessInvoiceRemindersCommandHandler> logger)
    {
        _invoices = invoices;
        _email = email;
        _links = links;
        _clock = clock;
        _logger = logger;
    }

    public async Task<InvoiceReminderRunDto> Handle(ProcessInvoiceRemindersCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var awaiting = await _invoices.ListAwaitingPaymentAsync(BatchSize, cancellationToken);
        int sent = 0, emailed = 0;

        foreach (var invoice in awaiting)
        {
            if (InvoiceRules.DueReminder(invoice, now) is not { } kind)
            {
                continue;
            }

            var delivered = false;
            if (_email.IsConfigured && !string.IsNullOrWhiteSpace(invoice.CustomerEmail))
            {
                var (subject, body) = BillingText.ReminderEmail(invoice, kind, _links.Invoice(invoice.PublicToken));
                try
                {
                    await _email.SendAsync(invoice.CustomerEmail, subject, body, cancellationToken);
                    delivered = true;
                }
                catch (Exception ex)
                {
                    // Not recorded, so the next run tries again.
                    _logger.LogWarning(ex, "Reminder {Kind} for invoice {Number} failed", kind, invoice.Number);
                    continue;
                }
            }

            // Recorded either way: without email the office sees the reminder is due and sends it themselves.
            var reminder = new InvoiceReminder { Id = Guid.NewGuid(), InvoiceId = invoice.Id, Kind = kind, SentAtUtc = now, Emailed = delivered };
            invoice.Reminders.Add(reminder);
            await _invoices.AddReminderAsync(reminder, cancellationToken);
            await _invoices.SaveChangesAsync(cancellationToken);
            sent++;
            if (delivered) emailed++;
        }

        if (sent > 0)
        {
            _logger.LogInformation("Payment reminders: {Sent} due, {Emailed} emailed ({Checked} invoices checked)", sent, emailed, awaiting.Count);
        }

        return new InvoiceReminderRunDto(awaiting.Count, sent, emailed);
    }
}
