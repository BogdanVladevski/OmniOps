using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _context;

    public AccountRepository(AppDbContext context) => _context = context;

    public Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        _context.UserAccounts.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<UserAccount?> GetDriverAccountAsync(Guid organizationId, Guid driverId, CancellationToken cancellationToken = default) =>
        _context.UserAccounts.FirstOrDefaultAsync(
            u => u.OrganizationId == organizationId && u.DriverId == driverId, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default) =>
        _context.Organizations.AnyAsync(o => o.Slug == slug, cancellationToken);

    public Task<DriverPairingCode?> GetPairingCodeAsync(string codeHash, CancellationToken cancellationToken = default) =>
        _context.DriverPairingCodes.FirstOrDefaultAsync(c => c.CodeHash == codeHash, cancellationToken);

    public async Task<Guid?> DefaultFleetIdAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await _context.Fleets.AsNoTracking()
            .Where(f => f.OrganizationId == organizationId)
            .OrderBy(f => f.CreatedAtUtc)
            .Select(f => (Guid?)f.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> OrganizationNameAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        _context.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.Name).FirstOrDefaultAsync(cancellationToken);

    public async Task AddOrganizationAsync(Organization organization, Workspace workspace, Fleet fleet, CancellationToken cancellationToken = default)
    {
        await _context.Organizations.AddAsync(organization, cancellationToken);
        await _context.Workspaces.AddAsync(workspace, cancellationToken);
        await _context.Fleets.AddAsync(fleet, cancellationToken);
    }

    public async Task AddAsync(UserAccount account, CancellationToken cancellationToken = default) =>
        await _context.UserAccounts.AddAsync(account, cancellationToken);

    public async Task AddPairingCodeAsync(DriverPairingCode code, CancellationToken cancellationToken = default) =>
        await _context.DriverPairingCodes.AddAsync(code, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
