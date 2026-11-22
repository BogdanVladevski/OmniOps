using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;

namespace OmniOps.Application.Dtos;

public record BillingProfileDto(
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
    bool EmailSendingEnabled)
{
    /// <summary>Name, address and a bank account are the minimum for an invoice a customer can pay.</summary>
    public static bool Complete(BillingProfile? p) =>
        p is not null && !string.IsNullOrWhiteSpace(p.LegalName) && !string.IsNullOrWhiteSpace(p.Address)
        && !string.IsNullOrWhiteSpace(p.Iban);

    public static BillingProfileDto From(BillingProfile? p, bool emailEnabled) => p is null
        ? new(string.Empty, string.Empty, null, null, null, null, null, "EUR", 0, 30, "INV", false, emailEnabled)
        : new(p.LegalName, p.Address, p.TaxId, p.Iban, p.BankName, p.Email, p.Phone, p.Currency,
            p.DefaultVatRatePercent, p.DefaultPaymentTermsDays, p.InvoicePrefix, Complete(p), emailEnabled);
}

public record InvoiceSummaryDto(
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

public record InvoiceLineDto(string Description, decimal Quantity, decimal UnitPrice, decimal VatRatePercent, decimal NetAmount, decimal VatAmount);

public record InvoiceReminderDto(string Kind, DateTime SentAtUtc, bool Emailed);

public record InvoiceDetailDto(
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
public record UninvoicedJobDto(
    JobDto Job,
    string SuggestedDescription,
    string? CustomerEmail,
    string? CustomerAddress,
    string? CustomerTaxId,
    int? CustomerPaymentTermsDays);

public record InvoiceOverviewDto(
    string Currency,
    decimal Outstanding,
    decimal Overdue,
    int OverdueCount,
    decimal PaidLast30Days,
    IReadOnlyList<InvoiceSummaryDto> Invoices,
    IReadOnlyList<UninvoicedJobDto> ToInvoice);

public record PublicInvoiceDocumentDto(Guid Id, string Kind, DateTime CapturedAtUtc, double? TemperatureCelsius, string? Note = null);

/// <summary>The customer's invoice page: the invoice plus the delivery's proof. Nothing internal.</summary>
public record PublicInvoiceDto(
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
    IReadOnlyList<PublicInvoiceDocumentDto> Documents)
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
            job?.Reference, job?.Origin, job?.Destination, job?.CompletedAtUtc, job?.RecipientName,
            job?.MinTempCelsius, job?.MaxTempCelsius,
            (job?.Documents ?? []).OrderBy(d => d.CapturedAtUtc)
                .Select(d => new PublicInvoiceDocumentDto(d.Id, d.Kind.ToString(), d.CapturedAtUtc, d.TemperatureCelsius, d.Note)).ToList());
    }
}

public record InvoiceReminderRunDto(int Checked, int Sent, int Emailed);
