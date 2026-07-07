using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetDemoStatusQueryHandler : IRequestHandler<GetDemoStatusQuery, DemoStatusDto>
{
    private static readonly Guid DefaultFleetId = Guid.Parse("f1000000-0000-0000-0000-000000000001");

    private readonly IFleetRepository _fleets;
    private readonly IVehicleRepository _vehicles;
    private readonly IIncidentRepository _incidents;
    private readonly ITenantRepository _tenants;

    public GetDemoStatusQueryHandler(
        IFleetRepository fleets,
        IVehicleRepository vehicles,
        IIncidentRepository incidents,
        ITenantRepository tenants)
    {
        _fleets = fleets;
        _vehicles = vehicles;
        _incidents = incidents;
        _tenants = tenants;
    }

    public async Task<DemoStatusDto> Handle(GetDemoStatusQuery request, CancellationToken cancellationToken)
    {
        var org = await _tenants.GetOrganizationAsync(TenantSeed.DefaultOrganizationId, cancellationToken);
        var fleet = await _fleets.GetByIdAsync(DefaultFleetId, cancellationToken);
        var vehicles = await _vehicles.GetByFleetIdAsync(DefaultFleetId, cancellationToken);
        var incidents = await _incidents.GetByFleetAsync(DefaultFleetId, IncidentStatus.Open, cancellationToken);

        return new DemoStatusDto(
            org?.Name ?? "OmniOps Demo Org",
            TenantSeed.DefaultOrganizationId,
            DefaultFleetId,
            fleet?.Name ?? "Cold-Chain North",
            vehicles.Count,
            incidents.Count,
            org?.Slug == "omniops-demo");
    }
}
