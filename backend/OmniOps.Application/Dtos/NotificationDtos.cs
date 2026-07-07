using OmniOps.Core.Entities;

namespace OmniOps.Application.Dtos;

public record NotificationDto(
    Guid Id,
    string Subject,
    string Body,
    string Channel,
    string Status,
    DateTime CreatedAtUtc)
{
    public static NotificationDto FromEntity(Notification n) =>
        new(n.Id, n.Subject, n.Body, n.Channel.ToString(), n.Status.ToString(), n.CreatedAtUtc);
}

public record NotificationPreferenceDto(
    string AlertType,
    bool EmailEnabled,
    bool PushEnabled,
    bool SmsEnabled,
    bool InAppEnabled);

public record AlertRuleDto(Guid Id, string Name, string AlertType, string Severity, bool IsEnabled);
