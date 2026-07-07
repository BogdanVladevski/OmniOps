using MediatR;
using OmniOps.Application.Commands;
using OmniOps.Application.Dtos;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

public class UpdateNotificationPreferencesCommandHandler
    : IRequestHandler<UpdateNotificationPreferencesCommand, NotificationPreferenceDto>
{
    private readonly INotificationRepository _notifications;
    private readonly ITenantContext _tenant;

    public UpdateNotificationPreferencesCommandHandler(INotificationRepository notifications, ITenantContext tenant)
    {
        _notifications = notifications;
        _tenant = tenant;
    }

    public async Task<NotificationPreferenceDto> Handle(
        UpdateNotificationPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _tenant.UserId ?? "anonymous";
        var pref = new NotificationPreference
        {
            Id = Guid.NewGuid(),
            OrganizationId = _tenant.OrganizationId,
            UserId = userId,
            AlertType = request.AlertType,
            EmailEnabled = request.EmailEnabled,
            PushEnabled = request.PushEnabled,
            SmsEnabled = request.SmsEnabled,
            InAppEnabled = request.InAppEnabled
        };
        await _notifications.UpsertPreferenceAsync(pref, cancellationToken);
        await _notifications.SaveChangesAsync(cancellationToken);
        return new NotificationPreferenceDto(
            pref.AlertType, pref.EmailEnabled, pref.PushEnabled, pref.SmsEnabled, pref.InAppEnabled);
    }
}

public class CreateAlertRuleCommandHandler : IRequestHandler<CreateAlertRuleCommand, AlertRuleDto>
{
    private readonly INotificationRepository _notifications;
    private readonly ITenantContext _tenant;

    public CreateAlertRuleCommandHandler(INotificationRepository notifications, ITenantContext tenant)
    {
        _notifications = notifications;
        _tenant = tenant;
    }

    public async Task<AlertRuleDto> Handle(CreateAlertRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = new AlertRule
        {
            Id = Guid.NewGuid(),
            OrganizationId = _tenant.OrganizationId,
            Name = request.Name.Trim(),
            AlertType = request.AlertType,
            Severity = request.Severity,
            NotifyEmail = request.NotifyEmail,
            NotifyPush = request.NotifyPush,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _notifications.AddAlertRuleAsync(rule, cancellationToken);
        await _notifications.SaveChangesAsync(cancellationToken);
        return new AlertRuleDto(rule.Id, rule.Name, rule.AlertType, rule.Severity, rule.IsEnabled);
    }
}
