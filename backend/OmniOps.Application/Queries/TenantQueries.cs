using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Queries;

public record GetOrganizationsQuery() : IRequest<IReadOnlyList<OrganizationDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.PlatformAdmin;
}

public record GetWorkspacesQuery(Guid OrganizationId) : IRequest<IReadOnlyList<WorkspaceDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetTeamsQuery(Guid OrganizationId) : IRequest<IReadOnlyList<TeamDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetTenantSettingsQuery(Guid OrganizationId) : IRequest<TenantSettingsDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}
