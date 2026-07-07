using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Commands;

public record ResolveIncidentCommand(Guid IncidentId, string? Notes) : IRequest<IncidentDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record AddIncidentNoteCommand(Guid IncidentId, string Text, string? Author = "operator")
    : IRequest<IncidentNoteDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record AssignIncidentCommand(Guid IncidentId, string AssignedTo) : IRequest<IncidentDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}
