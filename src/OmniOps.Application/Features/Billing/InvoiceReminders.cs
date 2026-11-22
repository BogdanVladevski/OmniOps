using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Domain.Billing;

namespace OmniOps.Application.Features.Billing;

/// <summary>System, hourly: sends the payment reminders that are due, across all companies.</summary>
public sealed record ProcessInvoiceRemindersCommand : ICommand<InvoiceReminderRunDto>;

public sealed class ProcessInvoiceRemindersHandler(
    IAppDbContext db, IEmailSender email, IPublicLinks links, TimeProvider clock, ILogger<ProcessInvoiceRemindersHandler> logger)
    : ICommandHandler<ProcessInvoiceRemindersCommand, InvoiceReminderRunDto>
{
    private const int BatchSize = 500;

    public async ValueTask<InvoiceReminderRunDto> Handle(ProcessInvoiceRemindersCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var awaiting = await db.Invoices.AcrossCompanies().Include(i => i.Reminders)
            .Where(i => i.Status == InvoiceStatus.Sent)
            .OrderBy(i => i.DueDateUtc).Take(BatchSize).ToListAsync(cancellationToken);
        int sent = 0, emailed = 0;

        foreach (var invoice in awaiting)
        {
            if (InvoiceRules.DueReminder(invoice, now) is not { } kind)
            {
                continue;
            }

            var delivered = false;
            if (email.IsConfigured && !string.IsNullOrWhiteSpace(invoice.CustomerEmail))
            {
                var (subject, body) = BillingText.ReminderEmail(invoice, kind, links.Invoice(invoice.PublicToken));
                try
                {
                    await email.SendAsync(invoice.CustomerEmail, subject, body, cancellationToken);
                    delivered = true;
                }
                catch (Exception ex)
                {
                    // Not recorded, so the next run tries again.
                    logger.LogWarning(ex, "Reminder {Kind} for invoice {Number} failed", kind, invoice.Number);
                    continue;
                }
            }

            // Recorded either way: without email the office sees the reminder is due and sends it themselves.
            var reminder = new InvoiceReminder { Id = Guid.NewGuid(), InvoiceId = invoice.Id, Kind = kind, SentAtUtc = now, Emailed = delivered };
            invoice.Reminders.Add(reminder);
            await db.SaveChangesAsync(cancellationToken);
            sent++;
            if (delivered)
            {
                emailed++;
            }
        }

        if (sent > 0)
        {
            logger.LogInformation("Payment reminders: {Sent} due, {Emailed} emailed ({Checked} invoices checked)", sent, emailed, awaiting.Count);
        }

        return new InvoiceReminderRunDto(awaiting.Count, sent, emailed);
    }
}
