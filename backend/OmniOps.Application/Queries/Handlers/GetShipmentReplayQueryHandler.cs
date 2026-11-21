using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetShipmentReplayQueryHandler : IRequestHandler<GetShipmentReplayQuery, ShipmentReplayDto?>
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ITelemetryRepository _telemetryRepository;
    private readonly ITenantContext _tenant;

    public GetShipmentReplayQueryHandler(
        IShipmentRepository shipmentRepository,
        ITelemetryRepository telemetryRepository,
        ITenantContext tenant)
    {
        _shipmentRepository = shipmentRepository;
        _telemetryRepository = telemetryRepository;
        _tenant = tenant;
    }

    public async Task<ShipmentReplayDto?> Handle(
        GetShipmentReplayQuery request,
        CancellationToken cancellationToken)
    {
        // Other organizations' shipments are indistinguishable from missing ones (404, not 403).
        var shipment = await _shipmentRepository.GetByIdForOrganizationAsync(
            request.ShipmentId, _tenant.OrganizationId, cancellationToken);
        if (shipment is null)
        {
            return null;
        }

        var fromUtc = request.FromUtc ?? request.AnchorUtc!.Value.AddMinutes(-5);
        var toUtc = request.ToUtc ?? request.AnchorUtc!.Value.AddMinutes(2);

        var telemetry = await _telemetryRepository.GetByVehicleInTimeRangeAsync(
            shipment.VehicleId,
            fromUtc,
            toUtc,
            cancellationToken);

        var shipmentInfo = new ShipmentInfoDto
        {
            ShipmentId = shipment.Id,
            ProductName = shipment.ProductName,
            BatchNumber = shipment.BatchNumber,
            ValueAtRiskUsd = shipment.ValueAtRiskUsd,
            MinSafeTempCelsius = shipment.MinSafeTempCelsius,
            MaxSafeTempCelsius = shipment.MaxSafeTempCelsius
        };

        return new ShipmentReplayDto
        {
            ShipmentId = shipment.Id,
            VehicleId = shipment.VehicleId,
            ProductName = shipment.ProductName,
            BatchNumber = shipment.BatchNumber,
            MinSafeTempCelsius = shipment.MinSafeTempCelsius,
            MaxSafeTempCelsius = shipment.MaxSafeTempCelsius,
            ValueAtRiskUsd = shipment.ValueAtRiskUsd,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Points = telemetry
                .Select(point => TelemetryDto.FromEntity(point, shipmentInfo))
                .ToList()
        };
    }
}
