using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

/// <summary>Describes the caller's own company (never another one) and whether it is the seeded demo.</summary>
public class GetDemoStatusQueryHandler : IRequestHandler<GetDemoStatusQuery, DemoStatusDto>
{
    private readonly IFleetRepository _fleets;
    private readonly IVehicleRepository _vehicles;
    private readonly IIncidentRepository _incidents;
    private readonly ITenantRepository _tenants;
    private readonly IAccountRepository _accounts;
    private readonly ITenantContext _tenant;

    public GetDemoStatusQueryHandler(
        IFleetRepository fleets,
        IVehicleRepository vehicles,
        IIncidentRepository incidents,
        ITenantRepository tenants,
        IAccountRepository accounts,
        ITenantContext tenant)
    {
        _fleets = fleets;
        _vehicles = vehicles;
        _incidents = incidents;
        _tenants = tenants;
        _accounts = accounts;
        _tenant = tenant;
    }

    public async Task<DemoStatusDto> Handle(GetDemoStatusQuery request, CancellationToken cancellationToken)
    {
        var organizationId = _tenant.OrganizationId;
        var org = await _tenants.GetOrganizationAsync(organizationId, cancellationToken);
        var fleetId = await _accounts.DefaultFleetIdAsync(organizationId, cancellationToken) ?? Guid.Empty;
        var fleet = await _fleets.GetByIdAsync(fleetId, cancellationToken);
        var vehicles = fleet is null ? [] : await _vehicles.GetByFleetIdAsync(fleetId, cancellationToken);
        var incidents = fleet is null ? [] : await _incidents.GetByFleetAsync(fleetId, IncidentStatus.Open, cancellationToken);

        return new DemoStatusDto(
            org?.Name ?? string.Empty,
            organizationId,
            fleetId,
            fleet?.Name ?? string.Empty,
            vehicles.Count,
            incidents.Count,
            org?.Slug == "omniops-demo");
    }
}
