using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Queries;

public record GetFleetAnalyticsQuery(Guid FleetId, DateTime FromUtc, DateTime ToUtc) : IRequest<FleetAnalyticsDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetDriverAnalyticsQuery(Guid FleetId, DateTime FromUtc, DateTime ToUtc) : IRequest<IReadOnlyList<DriverAnalyticsDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetOperationalAnalyticsQuery(Guid FleetId, DateTime FromUtc, DateTime ToUtc) : IRequest<OperationalAnalyticsDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}
