using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _context;

    public NotificationRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<Notification>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default) =>
        await _context.Notifications
            .Where(n => n.Status == NotificationStatus.Pending)
            .OrderBy(n => n.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetForUserAsync(
        string userId, Guid organizationId, int limit, CancellationToken cancellationToken = default) =>
        await _context.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.OrganizationId == organizationId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(
        string userId, Guid organizationId, CancellationToken cancellationToken = default) =>
        await _context.NotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == userId && p.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AlertRule>> GetAlertRulesAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await _context.AlertRules.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

    public Task AddAsync(Notification notification, CancellationToken cancellationToken = default) =>
        _context.Notifications.AddAsync(notification, cancellationToken).AsTask();

    public async Task UpsertPreferenceAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        var existing = await _context.NotificationPreferences.FirstOrDefaultAsync(
            p => p.UserId == preference.UserId
                 && p.OrganizationId == preference.OrganizationId
                 && p.AlertType == preference.AlertType,
            cancellationToken);
        if (existing is null)
            await _context.NotificationPreferences.AddAsync(preference, cancellationToken);
        else
        {
            existing.EmailEnabled = preference.EmailEnabled;
            existing.PushEnabled = preference.PushEnabled;
            existing.SmsEnabled = preference.SmsEnabled;
            existing.InAppEnabled = preference.InAppEnabled;
        }
    }

    public Task AddAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken = default) =>
        _context.AlertRules.AddAsync(rule, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
