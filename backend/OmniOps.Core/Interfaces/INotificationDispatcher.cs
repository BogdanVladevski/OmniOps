namespace OmniOps.Core.Interfaces;

public interface INotificationDispatcher
{
    Task EnqueueIncidentAlertAsync(
        Guid organizationId,
        string userId,
        string alertType,
        string title,
        string message,
        string? entityId = null,
        CancellationToken cancellationToken = default);
}
