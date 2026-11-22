using MediatR;
using OmniOps.Application.Dtos;

namespace OmniOps.Application.Queries;

public record GetDemoStatusQuery() : IRequest<DemoStatusDto>, OmniOps.Application.Abstractions.IAuthorizedRequest
{
    public string RequiredPolicy => OmniOps.Core.Authorization.AuthorizationPolicies.VehicleRead;
}
