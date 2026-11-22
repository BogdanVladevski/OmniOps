namespace OmniOps.UnitTests.Domain;

public class InvoiceRulesTests
{
    private static readonly DateTime Due = new(2026, 12, 15, 0, 0, 0, DateTimeKind.Utc);

    private static Invoice Sent(params ReminderKind[] already) => new()
    {
        Status = InvoiceStatus.Sent,
        DueDateUtc = Due,
        SentAtUtc = Due.AddDays(-30),
        Reminders = already.Select(k => new InvoiceReminder { Kind = k }).ToList(),
    };

    [Fact]
    public void Totals_AreTheSumOfLinesRoundedHalfAwayFromZero()
    {
        var invoice = new Invoice
        {
            Lines =
            [
                new InvoiceLine { Quantity = 1, UnitPrice = 10.005m, VatRatePercent = 18 },  // net 10.01 (half away), VAT 1.80
                new InvoiceLine { Quantity = 3, UnitPrice = 0.335m, VatRatePercent = 18 },   // net 1.01 (1.005 → 1.01), VAT 0.18
            ],
        };

        InvoiceRules.ApplyTotals(invoice);

        Assert.Equal(11.02m, invoice.Subtotal);
        Assert.Equal(1.98m, invoice.VatAmount);
        Assert.Equal(13.00m, invoice.Total);
    }

    [Fact]
    public void Number_HasPrefixYearAndPaddedSequence()
    {
        Assert.Equal("VT-2026-0007", InvoiceRules.FormatNumber("VT", 2026, 7));
        Assert.Equal("INV-2026-12345", InvoiceRules.FormatNumber("  ", 2026, 12345));
    }

    [Theory]
    [InlineData(-4, null)]                       // more than 3 days before due: nothing yet
    [InlineData(-3, ReminderKind.DueSoon)]
    [InlineData(0, ReminderKind.DueToday)]
    [InlineData(3, null)]                        // a few days late: wait for the week mark
    [InlineData(7, ReminderKind.Overdue7)]
    [InlineData(20, ReminderKind.Overdue14)]
    public void Reminder_FollowsTheSchedule(int daysAfterDue, ReminderKind? expected)
    {
        Assert.Equal(expected, InvoiceRules.DueReminder(Sent(), Due.AddDays(daysAfterDue).AddHours(9)));
    }

    [Fact]
    public void Reminder_EachStageGoesOutOnce()
    {
        Assert.Null(InvoiceRules.DueReminder(Sent(ReminderKind.Overdue7), Due.AddDays(9)));
        Assert.Equal(ReminderKind.Overdue14, InvoiceRules.DueReminder(Sent(ReminderKind.Overdue7), Due.AddDays(14)));
    }

    [Fact]
    public void Reminder_MissedEarlierStagesAreNotSentInABurst()
    {
        // Sent late: at 20 days overdue only the latest stage goes out, then nothing more.
        var invoice = Sent();
        Assert.Equal(ReminderKind.Overdue14, InvoiceRules.DueReminder(invoice, Due.AddDays(20)));
        invoice.Reminders.Add(new InvoiceReminder { Kind = ReminderKind.Overdue14 });
        Assert.Null(InvoiceRules.DueReminder(invoice, Due.AddDays(20)));
    }

    [Fact]
    public void Reminder_NeverForPaidOrUnsentInvoices()
    {
        var paid = Sent();
        paid.Status = InvoiceStatus.Paid;
        var notSent = Sent();
        notSent.Status = InvoiceStatus.Issued;

        Assert.Null(InvoiceRules.DueReminder(paid, Due.AddDays(10)));
        Assert.Null(InvoiceRules.DueReminder(notSent, Due.AddDays(10)));
    }

    [Fact]
    public void Reminder_NotWithinADayOfTheInvoiceBeingSent()
    {
        var invoice = Sent();
        invoice.SentAtUtc = Due.AddDays(-2).AddHours(-3);
        Assert.Null(InvoiceRules.DueReminder(invoice, Due.AddDays(-2)));
    }

    [Fact]
    public void Overdue_StartsTheDayAfterTheDueDate()
    {
        var invoice = Sent();
        Assert.False(InvoiceRules.IsOverdue(invoice, Due.AddHours(23)));
        Assert.True(InvoiceRules.IsOverdue(invoice, Due.AddDays(1)));
        Assert.Equal(5, InvoiceRules.DaysOverdue(invoice, Due.AddDays(5).AddHours(2)));
    }
}
