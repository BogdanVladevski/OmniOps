using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

public class NotificationDispatcher : INotificationDispatcher
{
    private readonly INotificationRepository _notifications;

    public NotificationDispatcher(INotificationRepository notifications) => _notifications = notifications;

    public async Task EnqueueIncidentAlertAsync(
        Guid organizationId,
        string userId,
        string alertType,
        string title,
        string message,
        string? entityId = null,
        CancellationToken cancellationToken = default)
    {
        var prefs = await _notifications.GetPreferencesAsync(userId, organizationId, cancellationToken);
        var pref = prefs.FirstOrDefault(p => p.AlertType == alertType);

        if (pref is null || pref.EmailEnabled)
        {
            await _notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                Channel = NotificationChannel.Email,
                Status = NotificationStatus.Pending,
                Subject = title,
                Body = message,
                RelatedEntityType = alertType,
                RelatedEntityId = entityId,
                CreatedAtUtc = DateTime.UtcNow
            }, cancellationToken);
        }

        if (pref is null || pref.PushEnabled)
        {
            await _notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                Channel = NotificationChannel.Push,
                Status = NotificationStatus.Pending,
                Subject = title,
                Body = message,
                RelatedEntityType = alertType,
                RelatedEntityId = entityId,
                CreatedAtUtc = DateTime.UtcNow
            }, cancellationToken);
        }

        await _notifications.SaveChangesAsync(cancellationToken);
    }
}
