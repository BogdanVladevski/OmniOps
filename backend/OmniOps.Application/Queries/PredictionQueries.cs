using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Core.Authorization;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries;

public record GetVehicleHealthQuery(string VehicleId) : IRequest<VehicleHealthPrediction>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetMaintenancePredictionQuery(string VehicleId) : IRequest<MaintenancePrediction>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetDriverRiskQuery(Guid DriverId) : IRequest<DriverRiskPrediction>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}
