using System.Globalization;
using OmniOps.Application.Features.Jobs;
using OmniOps.Domain.Billing;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Billing;

public sealed record BillingProfileDto(
    string LegalName,
    string Address,
    string? TaxId,
    string? Iban,
    string? BankName,
    string? Email,
    string? Phone,
    string Currency,
    decimal DefaultVatRatePercent,
    int DefaultPaymentTermsDays,
    string InvoicePrefix,
    bool IsComplete,
    bool EmailSendingEnabled,
    int FreeWaitingMinutes = 120,
    decimal? WaitingRatePerHour = null)
{
    /// <summary>Name, address and a bank account are the minimum for an invoice a customer can pay.</summary>
    public static bool Complete(BillingProfile? p) =>
        p is not null && !string.IsNullOrWhiteSpace(p.LegalName) && !string.IsNullOrWhiteSpace(p.Address)
        && !string.IsNullOrWhiteSpace(p.Iban);

    public static BillingProfileDto From(BillingProfile? p, bool emailEnabled) => p is null
        ? new(string.Empty, string.Empty, null, null, null, null, null, "EUR", 0, 30, "INV", false, emailEnabled)
        : new(p.LegalName, p.Address, p.TaxId, p.Iban, p.BankName, p.Email, p.Phone, p.Currency,
            p.DefaultVatRatePercent, p.DefaultPaymentTermsDays, p.InvoicePrefix, Complete(p), emailEnabled,
            p.FreeWaitingMinutes, p.WaitingRatePerHour);
}

public sealed record InvoiceSummaryDto(
    Guid Id,
    string Number,
    string Status,
    bool IsOverdue,
    int DaysOverdue,
    string CustomerName,
    string? CustomerEmail,
    string Currency,
    decimal Total,
    DateTime IssueDateUtc,
    DateTime DueDateUtc,
    DateTime? SentAtUtc,
    DateTime? PaidAtUtc,
    Guid? JobId,
    string PublicPath,
    int RemindersSent,
    DateTime? LastReminderAtUtc)
{
    public static InvoiceSummaryDto From(Invoice i, DateTime nowUtc) => new(
        i.Id, i.Number, i.Status.ToString(), InvoiceRules.IsOverdue(i, nowUtc), InvoiceRules.DaysOverdue(i, nowUtc),
        i.CustomerName, i.CustomerEmail, i.Currency, i.Total, i.IssueDateUtc, i.DueDateUtc, i.SentAtUtc, i.PaidAtUtc,
        i.JobId, $"/i/{i.PublicToken}", i.Reminders.Count, i.Reminders.Max(r => (DateTime?)r.SentAtUtc));
}

public sealed record InvoiceLineDto(string Description, decimal Quantity, decimal UnitPrice, decimal VatRatePercent, decimal NetAmount, decimal VatAmount);

public sealed record InvoiceReminderDto(string Kind, DateTime SentAtUtc, bool Emailed);

public sealed record InvoiceDetailDto(
    InvoiceSummaryDto Summary,
    decimal Subtotal,
    decimal VatAmount,
    IReadOnlyList<InvoiceLineDto> Lines,
    string? CustomerAddress,
    string? CustomerTaxId,
    string? Notes,
    IReadOnlyList<InvoiceReminderDto> Reminders,
    bool Emailed = false)
{
    public static InvoiceDetailDto From(Invoice i, DateTime nowUtc, bool emailed = false) => new(
        InvoiceSummaryDto.From(i, nowUtc),
        i.Subtotal,
        i.VatAmount,
        i.Lines.OrderBy(l => l.Position)
            .Select(l => new InvoiceLineDto(l.Description, l.Quantity, l.UnitPrice, l.VatRatePercent, l.NetAmount, l.VatAmount)).ToList(),
        i.CustomerAddress,
        i.CustomerTaxId,
        i.Notes,
        i.Reminders.OrderBy(r => r.SentAtUtc).Select(r => new InvoiceReminderDto(r.Kind.ToString(), r.SentAtUtc, r.Emailed)).ToList(),
        emailed);
}

/// <summary>A delivered job waiting for its invoice, with what we remember about the customer.</summary>
public sealed record UninvoicedJobDto(
    JobDto Job,
    string SuggestedDescription,
    string? CustomerEmail,
    string? CustomerAddress,
    string? CustomerTaxId,
    int? CustomerPaymentTermsDays,
    IReadOnlyList<WaitDto> Waits,
    IReadOnlyList<SuggestedLineDto> SuggestedLines);

public sealed record InvoiceOverviewDto(
    string Currency,
    decimal Outstanding,
    decimal Overdue,
    int OverdueCount,
    decimal PaidLast30Days,
    IReadOnlyList<InvoiceSummaryDto> Invoices,
    IReadOnlyList<UninvoicedJobDto> ToInvoice);

/// <summary>When the truck arrived at and was released from a stop, as recorded by the driver's phone.</summary>
public sealed record PublicStopTimeDto(string Stop, DateTime ArrivedAtUtc, DateTime? ReleasedAtUtc, string Source, bool Verified, int WaitedMinutes);

public sealed record PublicInvoiceDocumentDto(Guid Id, string Kind, DateTime CapturedAtUtc, double? TemperatureCelsius, string? Note = null);

/// <summary>The customer's invoice page: the invoice plus the delivery's proof. Nothing internal.</summary>
public sealed record PublicInvoiceDto(
    string Number,
    string Status,
    bool IsOverdue,
    DateTime IssueDateUtc,
    DateTime DueDateUtc,
    string Currency,
    decimal Subtotal,
    decimal VatAmount,
    decimal Total,
    IReadOnlyList<InvoiceLineDto> Lines,
    string SellerName,
    string SellerAddress,
    string? SellerTaxId,
    string? SellerIban,
    string? SellerBankName,
    string? SellerEmail,
    string CustomerName,
    string? CustomerAddress,
    string? CustomerTaxId,
    string? Notes,
    string? DeliveryReference,
    string? DeliveryOrigin,
    string? DeliveryDestination,
    DateTime? DeliveredAtUtc,
    string? ReceivedBy,
    double? MinTempCelsius,
    double? MaxTempCelsius,
    IReadOnlyList<PublicInvoiceDocumentDto> Documents,
    IReadOnlyList<PublicStopTimeDto>? StopTimes = null)
{
    public static PublicInvoiceDto From(Invoice i, DateTime nowUtc)
    {
        var job = i.Job;
        return new(
            i.Number, i.Status.ToString(), InvoiceRules.IsOverdue(i, nowUtc), i.IssueDateUtc, i.DueDateUtc, i.Currency,
            i.Subtotal, i.VatAmount, i.Total,
            i.Lines.OrderBy(l => l.Position)
                .Select(l => new InvoiceLineDto(l.Description, l.Quantity, l.UnitPrice, l.VatRatePercent, l.NetAmount, l.VatAmount)).ToList(),
            i.SellerName, i.SellerAddress, i.SellerTaxId, i.SellerIban, i.SellerBankName, i.SellerEmail,
            i.CustomerName, i.CustomerAddress, i.CustomerTaxId, i.Notes,
            job?.Reference, job?.Origin, job?.Destination, job?.DeliveredAtUtc, job?.RecipientName,
            job?.MinTempCelsius, job?.MaxTempCelsius,
            (job?.Documents ?? []).OrderBy(d => d.CapturedAtUtc)
                .Select(d => new PublicInvoiceDocumentDto(d.Id, d.Kind.ToString(), d.CapturedAtUtc, d.TemperatureCelsius, d.Note)).ToList(),
            job is null ? [] : WaitingTimePolicy.Waits(job, 0, null, nowUtc)
                .Select(w => new PublicStopTimeDto(w.Stop, w.ArrivedAtUtc, w.ReleasedAtUtc, w.Source.ToString(), w.Verified, w.WaitedMinutes))
                .ToList());
    }
}

public sealed record InvoiceReminderRunDto(int Checked, int Sent, int Emailed);

internal static class BillingText
{
    public static string Money(decimal amount, string currency) =>
        $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {currency}";

    public static string Day(DateTime utc) => utc.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string NormalizeName(string name) =>
        string.Join(' ', name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>"Transport Skopje depot → Bitola, order PO-2231, delivered 22 Nov 2026".</summary>
    public static string Describe(Job j)
    {
        var route = string.Join(" → ", new[] { j.Origin, j.Destination }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var parts = new List<string> { string.IsNullOrEmpty(route) ? "Transport" : $"Transport {route}" };
        if (!string.IsNullOrWhiteSpace(j.Reference))
        {
            parts.Add($"order {j.Reference}");
        }

        if (j.DeliveredAtUtc is { } at)
        {
            parts.Add($"delivered {Day(at)}");
        }

        return string.Join(", ", parts);
    }

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
