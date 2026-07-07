using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;
using OmniOps.Core.Enums;

namespace OmniOps.Application.Queries;

public record GetIncidentsQuery(Guid FleetId, IncidentStatus? Status = null) : IRequest<IReadOnlyList<IncidentDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}
