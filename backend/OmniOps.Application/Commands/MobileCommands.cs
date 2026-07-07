using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Commands;

public record RegisterPushTokenCommand(string PushToken, string Platform)
    : IRequest<bool>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}
