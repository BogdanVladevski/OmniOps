using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Commands;

public record CreateOrganizationCommand(string Name, string Slug) : IRequest<OrganizationDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.PlatformAdmin;
}

public record CreateWorkspaceCommand(Guid OrganizationId, string Name, Guid? DefaultFleetId)
    : IRequest<WorkspaceDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}

public record CreateTeamCommand(Guid OrganizationId, string Name) : IRequest<TeamDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}

public record InviteMemberCommand(Guid OrganizationId, string Email, string Role)
    : IRequest<InvitationDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}

public record UpdateTenantSettingsCommand(
    Guid OrganizationId,
    string TimeZone,
    string Locale,
    bool EmailNotificationsEnabled,
    bool PushNotificationsEnabled) : IRequest<TenantSettingsDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}
