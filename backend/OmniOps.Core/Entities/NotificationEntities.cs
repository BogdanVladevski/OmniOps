namespace OmniOps.Core.Entities;

public enum NotificationChannel
{
    InApp,
    Email,
    Push,
    Sms
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed,
    Skipped
}

public class Notification
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? Error { get; set; }
}

public class NotificationPreference
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public bool EmailEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; }
    public bool InAppEnabled { get; set; } = true;
}

public class AlertRule
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public bool IsEnabled { get; set; } = true;
    public bool NotifyEmail { get; set; } = true;
    public bool NotifyPush { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}

public class DeviceRegistration
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public string PushToken { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTime RegisteredAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
}

public class ApiKey
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool Revoked { get; set; }
}
