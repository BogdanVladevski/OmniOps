using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    private readonly INotificationRepository _notifications;
    private readonly ITenantContext _tenant;

    public GetNotificationsQueryHandler(INotificationRepository notifications, ITenantContext tenant)
    {
        _notifications = notifications;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = _tenant.UserId ?? "anonymous";
        var items = await _notifications.GetForUserAsync(userId, _tenant.OrganizationId, request.Limit, cancellationToken);
        return items.Select(NotificationDto.FromEntity).ToList();
    }
}

public class GetNotificationPreferencesQueryHandler
    : IRequestHandler<GetNotificationPreferencesQuery, IReadOnlyList<NotificationPreferenceDto>>
{
    private readonly INotificationRepository _notifications;
    private readonly ITenantContext _tenant;

    public GetNotificationPreferencesQueryHandler(INotificationRepository notifications, ITenantContext tenant)
    {
        _notifications = notifications;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> Handle(
        GetNotificationPreferencesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _tenant.UserId ?? "anonymous";
        var prefs = await _notifications.GetPreferencesAsync(userId, _tenant.OrganizationId, cancellationToken);
        return prefs.Select(p => new NotificationPreferenceDto(
            p.AlertType, p.EmailEnabled, p.PushEnabled, p.SmsEnabled, p.InAppEnabled)).ToList();
    }
}

public class GetAlertRulesQueryHandler : IRequestHandler<GetAlertRulesQuery, IReadOnlyList<AlertRuleDto>>
{
    private readonly INotificationRepository _notifications;
    private readonly ITenantContext _tenant;

    public GetAlertRulesQueryHandler(INotificationRepository notifications, ITenantContext tenant)
    {
        _notifications = notifications;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<AlertRuleDto>> Handle(GetAlertRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await _notifications.GetAlertRulesAsync(_tenant.OrganizationId, cancellationToken);
        return rules.Select(r => new AlertRuleDto(r.Id, r.Name, r.AlertType, r.Severity, r.IsEnabled)).ToList();
    }
}
