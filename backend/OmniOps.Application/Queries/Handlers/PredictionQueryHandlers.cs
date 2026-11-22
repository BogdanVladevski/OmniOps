using MediatR;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

// Each prediction first resolves the vehicle (or driver) inside the caller's organization:
// another organization's vehicle with the same external id is "not found".

public class GetVehicleHealthQueryHandler : IRequestHandler<GetVehicleHealthQuery, VehicleHealthPrediction>
{
    private readonly IPredictionService _predictions;
    private readonly ITenantScope _scope;

    public GetVehicleHealthQueryHandler(IPredictionService predictions, ITenantScope scope)
    {
        _predictions = predictions;
        _scope = scope;
    }

    public async Task<VehicleHealthPrediction> Handle(GetVehicleHealthQuery request, CancellationToken cancellationToken)
    {
        var fleetId = await _scope.FleetOfVehicleAsync(request.VehicleId, cancellationToken);
        return await _predictions.PredictVehicleHealthAsync(fleetId, request.VehicleId, cancellationToken);
    }
}

public class GetMaintenancePredictionQueryHandler : IRequestHandler<GetMaintenancePredictionQuery, MaintenancePrediction>
{
    private readonly IPredictionService _predictions;
    private readonly ITenantScope _scope;

    public GetMaintenancePredictionQueryHandler(IPredictionService predictions, ITenantScope scope)
    {
        _predictions = predictions;
        _scope = scope;
    }

    public async Task<MaintenancePrediction> Handle(GetMaintenancePredictionQuery request, CancellationToken cancellationToken)
    {
        var fleetId = await _scope.FleetOfVehicleAsync(request.VehicleId, cancellationToken);
        return await _predictions.PredictMaintenanceAsync(fleetId, request.VehicleId, cancellationToken);
    }
}

public class GetDriverRiskQueryHandler : IRequestHandler<GetDriverRiskQuery, DriverRiskPrediction>
{
    private readonly IPredictionService _predictions;
    private readonly ITenantScope _scope;

    public GetDriverRiskQueryHandler(IPredictionService predictions, ITenantScope scope)
    {
        _predictions = predictions;
        _scope = scope;
    }

    public async Task<DriverRiskPrediction> Handle(GetDriverRiskQuery request, CancellationToken cancellationToken)
    {
        await _scope.EnsureDriverAsync(request.DriverId, cancellationToken);
        return await _predictions.PredictDriverRiskAsync(request.DriverId, cancellationToken);
    }
}
