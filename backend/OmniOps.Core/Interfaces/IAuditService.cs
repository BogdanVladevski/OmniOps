namespace OmniOps.Core.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string entityType, string? entityId, string? userId, string? details,
        CancellationToken cancellationToken = default);

    /// <summary>For actions taken before sign-in (accepting an invitation, resetting a password): the organization is given.</summary>
    Task LogForAsync(Guid organizationId, string action, string entityType, string? entityId, string? userId, string? details,
        CancellationToken cancellationToken = default);
}
