using MediatR;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetVehicleHealthQueryHandler : IRequestHandler<GetVehicleHealthQuery, VehicleHealthPrediction>
{
    private readonly IPredictionService _predictions;

    public GetVehicleHealthQueryHandler(IPredictionService predictions) => _predictions = predictions;

    public Task<VehicleHealthPrediction> Handle(GetVehicleHealthQuery request, CancellationToken cancellationToken) =>
        _predictions.PredictVehicleHealthAsync(request.VehicleId, cancellationToken);
}

public class GetMaintenancePredictionQueryHandler : IRequestHandler<GetMaintenancePredictionQuery, MaintenancePrediction>
{
    private readonly IPredictionService _predictions;

    public GetMaintenancePredictionQueryHandler(IPredictionService predictions) => _predictions = predictions;

    public Task<MaintenancePrediction> Handle(GetMaintenancePredictionQuery request, CancellationToken cancellationToken) =>
        _predictions.PredictMaintenanceAsync(request.VehicleId, cancellationToken);
}

public class GetDriverRiskQueryHandler : IRequestHandler<GetDriverRiskQuery, DriverRiskPrediction>
{
    private readonly IPredictionService _predictions;

    public GetDriverRiskQueryHandler(IPredictionService predictions) => _predictions = predictions;

    public Task<DriverRiskPrediction> Handle(GetDriverRiskQuery request, CancellationToken cancellationToken) =>
        _predictions.PredictDriverRiskAsync(request.DriverId, cancellationToken);
}
