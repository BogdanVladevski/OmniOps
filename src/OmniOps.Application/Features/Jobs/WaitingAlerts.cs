using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Jobs;

/// <summary>System, every few minutes: tells each office when a truck's free waiting time has run out (once per stop).</summary>
public sealed record ProcessWaitingAlertsCommand : ICommand<int>;

public sealed class ProcessWaitingAlertsHandler(
    IAppDbContext db, IRealtimeNotifier notifier, TimeProvider clock, ILogger<ProcessWaitingAlertsHandler> logger)
    : ICommandHandler<ProcessWaitingAlertsCommand, int>
{
    public async ValueTask<int> Handle(ProcessWaitingAlertsCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var waiting = await db.Jobs.AcrossCompanies().Include(j => j.Vehicle)
            .Where(j => (j.Status == JobStatus.Planned && j.PickupArrivedAtUtc != null && j.PickupWaitAlertedAtUtc == null)
                || (j.Status == JobStatus.InProgress && j.DeliveryArrivedAtUtc != null && j.DeliveryWaitAlertedAtUtc == null))
            .Take(500)
            .ToListAsync(cancellationToken);

        var sent = 0;
        var terms = new Dictionary<Guid, WaitingTerms>();
        foreach (var job in waiting)
        {
            if (!terms.TryGetValue(job.OrganizationId, out var t))
            {
                terms[job.OrganizationId] = t = await WaitingTerms.ForAsync(db, job.OrganizationId, cancellationToken);
            }

            foreach (var wait in WaitingTimePolicy.Waits(job, t.FreeMinutes, t.RatePerHour, now))
            {
                var pickup = wait.Stop == WaitingTimePolicy.Pickup;
                if (!wait.Running || (pickup ? job.PickupWaitAlertedAtUtc : job.DeliveryWaitAlertedAtUtc) is not null || now < wait.ChargedFromUtc)
                {
                    continue;
                }

                if (pickup)
                {
                    job.PickupWaitAlertedAtUtc = now;
                }
                else
                {
                    job.DeliveryWaitAlertedAtUtc = now;
                }

                // Saved first: the alert must not repeat if sending it or a later job fails.
                await db.SaveChangesAsync(cancellationToken);
                sent++;
                try
                {
                    var where = (pickup ? job.Origin : job.Destination) ?? (pickup ? "the pickup" : "the customer");
                    await notifier.AlertAsync(job.OrganizationId, JobAlerts.WaitingCharged,
                        $"Free waiting time is over: {job.Vehicle.Name} at {where}",
                        $"{job.CustomerName ?? where}: waiting {WaitingTerms.Duration(wait.WaitedMinutes)}."
                        + (t.RatePerHour is { } rate ? $" Waiting is now charged at {rate:0.##} {t.Currency}/h." : string.Empty),
                        job.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Waiting alert failed for job {JobId}", job.Id);
                }
            }
        }

        return sent;
    }
}
