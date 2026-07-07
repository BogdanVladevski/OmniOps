namespace OmniOps.Core.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string entityType, string? entityId, string? userId, string? details,
        CancellationToken cancellationToken = default);
}
