using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface ITenantRepository
{
    Task<Organization?> GetOrganizationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Organization>> GetOrganizationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Workspace>> GetWorkspacesAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> GetTeamsAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<Invitation?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<TenantSettings?> GetSettingsAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task AddOrganizationAsync(Organization organization, CancellationToken cancellationToken = default);
    Task AddWorkspaceAsync(Workspace workspace, CancellationToken cancellationToken = default);
    Task AddTeamAsync(Team team, CancellationToken cancellationToken = default);
    Task AddTeamMemberAsync(TeamMember member, CancellationToken cancellationToken = default);
    Task AddInvitationAsync(Invitation invitation, CancellationToken cancellationToken = default);
    Task UpsertSettingsAsync(TenantSettings settings, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
