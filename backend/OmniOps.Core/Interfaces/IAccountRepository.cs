using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IAccountRepository
{
    Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetDriverAccountAsync(Guid organizationId, Guid driverId, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default);
    Task<DriverPairingCode?> GetPairingCodeAsync(string codeHash, CancellationToken cancellationToken = default);
    Task<Guid?> DefaultFleetIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<string?> OrganizationNameAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<Organization?> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>Office people (owners and dispatchers) of one organization, active or not.</summary>
    Task<IReadOnlyList<UserAccount>> ListOfficeAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<int> CountActiveOwnersAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<AccountLink?> GetLinkAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<AccountLink?> GetLinkAsync(Guid organizationId, Guid linkId, CancellationToken cancellationToken = default);

    /// <summary>Invitations of one organization that can still be accepted.</summary>
    Task<IReadOnlyList<AccountLink>> ListOpenInvitationsAsync(Guid organizationId, DateTime now, CancellationToken cancellationToken = default);

    Task AddLinkAsync(AccountLink link, CancellationToken cancellationToken = default);

    Task AddOrganizationAsync(Organization organization, Workspace workspace, Fleet fleet, CancellationToken cancellationToken = default);
    Task AddAsync(UserAccount account, CancellationToken cancellationToken = default);
    Task AddPairingCodeAsync(DriverPairingCode code, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
