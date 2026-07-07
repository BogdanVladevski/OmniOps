using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetIncidentsQueryHandler : IRequestHandler<GetIncidentsQuery, IReadOnlyList<IncidentDto>>
{
    private readonly IIncidentRepository _incidents;

    public GetIncidentsQueryHandler(IIncidentRepository incidents) => _incidents = incidents;

    public async Task<IReadOnlyList<IncidentDto>> Handle(GetIncidentsQuery request, CancellationToken cancellationToken)
    {
        var items = await _incidents.GetByFleetAsync(request.FleetId, request.Status, cancellationToken);
        return items.Select(IncidentDto.FromEntity).ToList();
    }
}
