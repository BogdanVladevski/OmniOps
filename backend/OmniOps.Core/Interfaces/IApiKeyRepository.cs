using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IApiKeyRepository
{
    Task<IReadOnlyList<ApiKey>> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<ApiKey?> GetByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
    Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
