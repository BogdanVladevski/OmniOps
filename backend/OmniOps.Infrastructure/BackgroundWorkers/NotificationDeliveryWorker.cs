using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.BackgroundWorkers;

public class NotificationDeliveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDeliveryWorker> _logger;

    public NotificationDeliveryWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationDeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
                var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                var push = scope.ServiceProvider.GetRequiredService<IPushSender>();
                var devices = scope.ServiceProvider.GetRequiredService<IDeviceRegistrationRepository>();

                var pending = await notifications.GetPendingAsync(25, stoppingToken);
                foreach (var item in pending)
                {
                    try
                    {
                        if (item.Channel == NotificationChannel.Email)
                            await email.SendAsync(item.UserId, item.Subject, item.Body, stoppingToken);
                        else if (item.Channel == NotificationChannel.Push)
                        {
                            var registrations = await devices.GetByUserAsync(item.UserId, stoppingToken);
                            foreach (var reg in registrations)
                                await push.SendAsync(reg.PushToken, item.Subject, item.Body, stoppingToken);
                        }
                        else if (item.Channel == NotificationChannel.Sms)
                        {
                            item.Status = NotificationStatus.Skipped;
                            item.Error = "SMS not configured";
                            continue;
                        }

                        item.Status = NotificationStatus.Sent;
                        item.SentAtUtc = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        item.Status = NotificationStatus.Failed;
                        item.Error = ex.Message;
                        _logger.LogWarning(ex, "Failed to deliver notification {NotificationId}", item.Id);
                    }
                }

                if (pending.Count > 0)
                    await notifications.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification delivery worker error");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
