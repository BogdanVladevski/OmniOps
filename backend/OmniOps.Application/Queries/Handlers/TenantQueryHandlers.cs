using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetOrganizationsQueryHandler : IRequestHandler<GetOrganizationsQuery, IReadOnlyList<OrganizationDto>>
{
    private readonly ITenantRepository _tenantRepository;

    public GetOrganizationsQueryHandler(ITenantRepository tenantRepository) => _tenantRepository = tenantRepository;

    public async Task<IReadOnlyList<OrganizationDto>> Handle(GetOrganizationsQuery request, CancellationToken cancellationToken)
    {
        var orgs = await _tenantRepository.GetOrganizationsAsync(cancellationToken);
        return orgs.Select(OrganizationDto.FromEntity).ToList();
    }
}

public class GetWorkspacesQueryHandler : IRequestHandler<GetWorkspacesQuery, IReadOnlyList<WorkspaceDto>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantContext _tenant;

    public GetWorkspacesQueryHandler(ITenantRepository tenantRepository, ITenantContext tenant)
    {
        _tenantRepository = tenantRepository;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<WorkspaceDto>> Handle(GetWorkspacesQuery request, CancellationToken cancellationToken)
    {
        if (request.OrganizationId != _tenant.OrganizationId)
            throw new UnauthorizedAccessException("Cross-tenant workspace access is not allowed.");

        var workspaces = await _tenantRepository.GetWorkspacesAsync(request.OrganizationId, cancellationToken);
        return workspaces.Select(WorkspaceDto.FromEntity).ToList();
    }
}

public class GetTeamsQueryHandler : IRequestHandler<GetTeamsQuery, IReadOnlyList<TeamDto>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantContext _tenant;

    public GetTeamsQueryHandler(ITenantRepository tenantRepository, ITenantContext tenant)
    {
        _tenantRepository = tenantRepository;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<TeamDto>> Handle(GetTeamsQuery request, CancellationToken cancellationToken)
    {
        if (request.OrganizationId != _tenant.OrganizationId)
            throw new UnauthorizedAccessException("Cross-tenant team access is not allowed.");

        var teams = await _tenantRepository.GetTeamsAsync(request.OrganizationId, cancellationToken);
        return teams.Select(t => new TeamDto(t.Id, t.OrganizationId, t.Name, t.Members.Count, t.CreatedAtUtc)).ToList();
    }
}

public class GetTenantSettingsQueryHandler : IRequestHandler<GetTenantSettingsQuery, TenantSettingsDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantContext _tenant;

    public GetTenantSettingsQueryHandler(ITenantRepository tenantRepository, ITenantContext tenant)
    {
        _tenantRepository = tenantRepository;
        _tenant = tenant;
    }

    public async Task<TenantSettingsDto> Handle(GetTenantSettingsQuery request, CancellationToken cancellationToken)
    {
        if (request.OrganizationId != _tenant.OrganizationId)
            throw new UnauthorizedAccessException("Cross-tenant settings access is not allowed.");

        var settings = await _tenantRepository.GetSettingsAsync(request.OrganizationId, cancellationToken)
            ?? new Core.Entities.TenantSettings { OrganizationId = request.OrganizationId };
        return new TenantSettingsDto(
            settings.OrganizationId,
            settings.TimeZone,
            settings.Locale,
            settings.EmailNotificationsEnabled,
            settings.PushNotificationsEnabled);
    }
}
