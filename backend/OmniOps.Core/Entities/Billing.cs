using OmniOps.Core.Enums;

namespace OmniOps.Core.Entities;

/// <summary>The company's invoicing details (one per organization). Printed on every invoice.</summary>
public class BillingProfile
{
    public Guid OrganizationId { get; set; }
    public string LegalName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? Iban { get; set; }
    public string? BankName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal DefaultVatRatePercent { get; set; }
    public int DefaultPaymentTermsDays { get; set; } = 30;

    /// <summary>Waiting at a pickup or delivery that is included in the price.</summary>
    public int FreeWaitingMinutes { get; set; } = 120;

    /// <summary>Charged per started 15 minutes beyond the free time. Null: waiting is recorded but not charged.</summary>
    public decimal? WaitingRatePerHour { get; set; }

    /// <summary>IANA time zone of the company (e.g. "Europe/Skopje"), for times written on invoices.</summary>
    public string? TimeZone { get; set; }
    public string InvoicePrefix { get; set; } = "INV";

    /// <summary>
    /// Next sequence number for this organization's invoices. Taken with a locking UPDATE inside the
    /// invoice's transaction, so numbers are unique and gapless (a failed invoice gives its number back).
    /// </summary>
    public int NextInvoiceSequence { get; set; } = 1;

    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Remembered billing details for a customer, used to pre-fill the next invoice.</summary>
public class Customer
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Lower-cased, trimmed name used to match jobs to customers.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxId { get; set; }
    public int? PaymentTermsDays { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class Invoice
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? JobId { get; set; }

    /// <summary>e.g. "INV-2026-0007"; unique per organization, never reused.</summary>
    public string Number { get; set; } = string.Empty;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;
    public DateTime IssueDateUtc { get; set; }
    public DateTime DueDateUtc { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal Subtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }

    // Snapshot of both parties at issue time: later edits to profiles never change an issued invoice.
    public string SellerName { get; set; } = string.Empty;
    public string SellerAddress { get; set; } = string.Empty;
    public string? SellerTaxId { get; set; }
    public string? SellerIban { get; set; }
    public string? SellerBankName { get; set; }
    public string? SellerEmail { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerTaxId { get; set; }
    public string? Notes { get; set; }

    /// <summary>Unguessable token for the customer's invoice page.</summary>
    public string PublicToken { get; set; } = string.Empty;

    public DateTime? SentAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Trip? Job { get; set; }
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<InvoiceReminder> Reminders { get; set; } = new List<InvoiceReminder>();
}

public class InvoiceLine
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public int Position { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRatePercent { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
}

/// <summary>A payment reminder that went out (or would have, without an email address). One per kind per invoice.</summary>
public class InvoiceReminder
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public ReminderKind Kind { get; set; }
    public DateTime SentAtUtc { get; set; }

    /// <summary>False when the customer has no email: the office is shown the reminder is due instead.</summary>
    public bool Emailed { get; set; }
}
