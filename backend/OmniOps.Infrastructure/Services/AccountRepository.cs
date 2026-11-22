using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
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

    public Task<Organization?> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        _context.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

    public async Task<IReadOnlyList<UserAccount>> ListOfficeAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await _context.UserAccounts
            .Where(u => u.OrganizationId == organizationId && u.Role != UserRole.Driver)
            .OrderBy(u => u.Role).ThenBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

    public Task<int> CountActiveOwnersAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        _context.UserAccounts.CountAsync(
            u => u.OrganizationId == organizationId && u.Role == UserRole.Owner && u.IsActive, cancellationToken);

    public Task<AccountLink?> GetLinkAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.AccountLinks.FirstOrDefaultAsync(l => l.TokenHash == tokenHash, cancellationToken);

    public Task<AccountLink?> GetLinkAsync(Guid organizationId, Guid linkId, CancellationToken cancellationToken = default) =>
        _context.AccountLinks.FirstOrDefaultAsync(l => l.OrganizationId == organizationId && l.Id == linkId, cancellationToken);

    public async Task<IReadOnlyList<AccountLink>> ListOpenInvitationsAsync(Guid organizationId, DateTime now, CancellationToken cancellationToken = default) =>
        await _context.AccountLinks
            .Where(l => l.OrganizationId == organizationId && l.Purpose == AccountLinkPurpose.Invitation
                && l.UsedAtUtc == null && l.RevokedAtUtc == null && l.ExpiresAtUtc > now)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddLinkAsync(AccountLink link, CancellationToken cancellationToken = default) =>
        await _context.AccountLinks.AddAsync(link, cancellationToken);

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
