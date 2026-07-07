using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetForUserAsync(
        string userId, Guid organizationId, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(
        string userId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertRule>> GetAlertRulesAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task UpsertPreferenceAsync(NotificationPreference preference, CancellationToken cancellationToken = default);
    Task AddAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
