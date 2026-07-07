using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Commands;

public record CopilotAskCommand(string Question, Guid FleetId) : IRequest<CopilotResponseDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}
