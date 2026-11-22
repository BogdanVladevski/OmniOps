using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OmniOps.Infrastructure.BackgroundJobs;

/// <summary>Sends a system command on a fixed interval, each run in its own scope. A failed run is logged and retried next time.</summary>
internal abstract class ScheduledCommandService<TCommand>(IServiceScopeFactory scopes, ILogger logger, TimeSpan interval) : BackgroundService
    where TCommand : IMessage, new()
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting (and migrating) first.
        using var timer = new PeriodicTimer(interval);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new TCommand(), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Scheduled {Command} failed", typeof(TCommand).Name);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }
}

/// <summary>Hourly: payment reminders that are due.</summary>
internal sealed class InvoiceReminderService(IServiceScopeFactory scopes, ILogger<InvoiceReminderService> logger)
    : ScheduledCommandService<Application.Features.Billing.ProcessInvoiceRemindersCommand>(scopes, logger, TimeSpan.FromHours(1));

/// <summary>Every 2 minutes: trucks whose free waiting time just ran out.</summary>
internal sealed class WaitingAlertService(IServiceScopeFactory scopes, ILogger<WaitingAlertService> logger)
    : ScheduledCommandService<Application.Features.Jobs.ProcessWaitingAlertsCommand>(scopes, logger, TimeSpan.FromMinutes(2));
