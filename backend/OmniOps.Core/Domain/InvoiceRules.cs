using OmniOps.Core.Entities;
using OmniOps.Core.Enums;

namespace OmniOps.Core.Domain;

/// <summary>Invoice arithmetic, numbering and the reminder schedule. Pure functions only.</summary>
public static class InvoiceRules
{
    /// <summary>Money is rounded per line to 2 decimals, half away from zero (the usual commercial rounding).</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static (decimal Net, decimal Vat) LineAmounts(decimal quantity, decimal unitPrice, decimal vatRatePercent)
    {
        var net = Round(quantity * unitPrice);
        return (net, Round(net * vatRatePercent / 100m));
    }

    /// <summary>Fills each line's amounts and the invoice totals. Totals are the sum of rounded lines.</summary>
    public static void ApplyTotals(Invoice invoice)
    {
        foreach (var line in invoice.Lines)
        {
            (line.NetAmount, line.VatAmount) = LineAmounts(line.Quantity, line.UnitPrice, line.VatRatePercent);
        }

        invoice.Subtotal = invoice.Lines.Sum(l => l.NetAmount);
        invoice.VatAmount = invoice.Lines.Sum(l => l.VatAmount);
        invoice.Total = invoice.Subtotal + invoice.VatAmount;
    }

    /// <summary>"INV-2026-0007": prefix, year of issue, sequence padded to 4 digits.</summary>
    public static string FormatNumber(string prefix, int year, int sequence) =>
        $"{(string.IsNullOrWhiteSpace(prefix) ? "INV" : prefix.Trim())}-{year}-{sequence:D4}";

    public static bool IsOverdue(Invoice invoice, DateTime nowUtc) =>
        invoice.Status != InvoiceStatus.Paid && nowUtc.Date > invoice.DueDateUtc.Date;

    public static int DaysOverdue(Invoice invoice, DateTime nowUtc) =>
        IsOverdue(invoice, nowUtc) ? (nowUtc.Date - invoice.DueDateUtc.Date).Days : 0;

    /// <summary>
    /// The reminder to send now, if any: the most advanced stage reached that has not gone out yet.
    /// Only sent invoices that are unpaid get reminders; earlier stages missed (e.g. invoice sent late)
    /// are skipped rather than sent in a burst.
    /// </summary>
    public static ReminderKind? DueReminder(Invoice invoice, DateTime nowUtc)
    {
        if (invoice.Status != InvoiceStatus.Sent)
        {
            return null;
        }

        var daysToDue = (invoice.DueDateUtc.Date - nowUtc.Date).Days;
        ReminderKind? stage = daysToDue switch
        {
            <= -14 => ReminderKind.Overdue14,
            <= -7 => ReminderKind.Overdue7,
            0 => ReminderKind.DueToday,
            > 0 and <= 3 => ReminderKind.DueSoon,
            _ => null,
        };

        // A stage reached after a later one already went out must not send again (e.g. the 7-day note after the 14-day).
        if (stage is null || invoice.Reminders.Any(r => r.Kind >= stage))
        {
            return null;
        }

        // Never remind about an invoice the customer received less than a day ago.
        return invoice.SentAtUtc is { } sent && nowUtc - sent < TimeSpan.FromDays(1) ? null : stage;
    }
}
