using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetIncidentsQueryHandler : IRequestHandler<GetIncidentsQuery, IReadOnlyList<IncidentDto>>
{
    private readonly IIncidentRepository _incidents;
    private readonly ITenantScope _scope;

    public GetIncidentsQueryHandler(IIncidentRepository incidents, ITenantScope scope)
    {
        _incidents = incidents;
        _scope = scope;
    }

    public async Task<IReadOnlyList<IncidentDto>> Handle(GetIncidentsQuery request, CancellationToken cancellationToken)
    {
        await _scope.EnsureFleetAsync(request.FleetId, cancellationToken);
        var items = await _incidents.GetByFleetAsync(request.FleetId, request.Status, cancellationToken);
        return items.Select(IncidentDto.FromEntity).ToList();
    }
}
