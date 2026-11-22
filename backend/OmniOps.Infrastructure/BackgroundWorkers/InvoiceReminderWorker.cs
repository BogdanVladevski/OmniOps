using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniOps.Application.Commands;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Infrastructure.BackgroundWorkers;

/// <summary>Sends due payment reminders on a schedule (hourly by default).</summary>
public class InvoiceReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval;
    private readonly ILogger<InvoiceReminderWorker> _logger;

    public InvoiceReminderWorker(IServiceScopeFactory scopeFactory, IOptions<InvoiceReminderOptions> options, ILogger<InvoiceReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes));
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting (and migrating) first.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ProcessInvoiceRemindersCommand(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment reminder run failed");
            }

            try { await Task.Delay(_interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
