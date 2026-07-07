using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly AppDbContext _context;

    public ApiKeyRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<ApiKey>> GetByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await _context.ApiKeys.AsNoTracking()
            .Where(k => k.OrganizationId == organizationId)
            .OrderByDescending(k => k.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<ApiKey?> GetByPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
        _context.ApiKeys.FirstOrDefaultAsync(k => k.KeyPrefix == prefix && !k.Revoked, cancellationToken);

    public Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default) =>
        _context.ApiKeys.AddAsync(apiKey, cancellationToken).AsTask();

    public async Task RevokeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (key is not null) key.Revoked = true;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
