using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class TenantRepository : ITenantRepository
{
    private readonly AppDbContext _context;

    public TenantRepository(AppDbContext context) => _context = context;

    public Task<Organization?> GetOrganizationAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Organization>> GetOrganizationsAsync(CancellationToken cancellationToken = default) =>
        await _context.Organizations.AsNoTracking().OrderBy(o => o.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await _context.Workspaces.AsNoTracking()
            .Where(w => w.OrganizationId == organizationId)
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Team>> GetTeamsAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await _context.Teams.AsNoTracking()
            .Include(t => t.Members)
            .Where(t => t.OrganizationId == organizationId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

    public Task<Invitation?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.Invitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public Task<TenantSettings?> GetSettingsAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        _context.TenantSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken);

    public Task AddOrganizationAsync(Organization organization, CancellationToken cancellationToken = default) =>
        _context.Organizations.AddAsync(organization, cancellationToken).AsTask();

    public Task AddWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default) =>
        _context.Workspaces.AddAsync(workspace, cancellationToken).AsTask();

    public Task AddTeamAsync(Team team, CancellationToken cancellationToken = default) =>
        _context.Teams.AddAsync(team, cancellationToken).AsTask();

    public Task AddTeamMemberAsync(TeamMember member, CancellationToken cancellationToken = default) =>
        _context.TeamMembers.AddAsync(member, cancellationToken).AsTask();

    public Task AddInvitationAsync(Invitation invitation, CancellationToken cancellationToken = default) =>
        _context.Invitations.AddAsync(invitation, cancellationToken).AsTask();

    public async Task UpsertSettingsAsync(TenantSettings settings, CancellationToken cancellationToken = default)
    {
        var existing = await _context.TenantSettings.FirstOrDefaultAsync(
            s => s.OrganizationId == settings.OrganizationId, cancellationToken);
        if (existing is null)
            await _context.TenantSettings.AddAsync(settings, cancellationToken);
        else
        {
            existing.TimeZone = settings.TimeZone;
            existing.Locale = settings.Locale;
            existing.EmailNotificationsEnabled = settings.EmailNotificationsEnabled;
            existing.PushNotificationsEnabled = settings.PushNotificationsEnabled;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
