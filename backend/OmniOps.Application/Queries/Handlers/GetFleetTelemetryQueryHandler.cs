using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetFleetTelemetryQueryHandler : IRequestHandler<GetFleetTelemetryQuery, FleetTelemetryDto>
{
    private readonly ITelemetryCacheService _cacheService;
    private readonly IFleetVehicleRegistry _fleetRegistry;
    private readonly IShipmentRepository _shipmentRepository;

    public GetFleetTelemetryQueryHandler(
        ITelemetryCacheService cacheService,
        IFleetVehicleRegistry fleetRegistry,
        IShipmentRepository shipmentRepository)
    {
        _cacheService = cacheService;
        _fleetRegistry = fleetRegistry;
        _shipmentRepository = shipmentRepository;
    }

    public async Task<FleetTelemetryDto> Handle(
        GetFleetTelemetryQuery request,
        CancellationToken cancellationToken)
    {
        var vehicleIds = _fleetRegistry.GetConfiguredVehicleIds();
        var vehicles = new List<TelemetryDto>();

        foreach (var vehicleId in vehicleIds)
        {
            var telemetry = await _cacheService.GetLatestTelemetryAsync(vehicleId);
            if (telemetry is null)
            {
                continue;
            }

            ShipmentInfoDto? shipmentInfo = null;
            try
            {
                var shipment = await _shipmentRepository.GetActiveShipmentForVehicleAsync(
                    vehicleId, cancellationToken);

                if (shipment is not null)
                {
                    // ExcursionDurationSeconds is not tracked here (Redis anomaly service owns that);
                    // we set it to 0 in the fleet snapshot — the live value comes via SignalR alerts.
                    shipmentInfo = new ShipmentInfoDto
                    {
                        ShipmentId = shipment.Id,
                        ProductName = shipment.ProductName,
                        BatchNumber = shipment.BatchNumber,
                        ValueAtRiskUsd = shipment.ValueAtRiskUsd,
                        MinSafeTempCelsius = shipment.MinSafeTempCelsius,
                        MaxSafeTempCelsius = shipment.MaxSafeTempCelsius,
                        ExcursionDurationSeconds = 0
                    };
                }
            }
            catch
            {
                // Shipment lookup failure must never break the fleet snapshot.
            }

            vehicles.Add(TelemetryDto.FromEntity(telemetry, shipmentInfo));
        }

        return new FleetTelemetryDto
        {
            Vehicles = vehicles,
            Summary = BuildSummary(vehicleIds.Count, vehicles)
        };
    }

    private static FleetSummaryDto BuildSummary(int configuredCount, IReadOnlyList<TelemetryDto> vehicles)
    {
        if (vehicles.Count == 0)
        {
            return new FleetSummaryDto
            {
                ConfiguredVehicleCount = configuredCount,
                ActiveVehicleCount = 0,
                WarningCount = 0
            };
        }

        var warningCount = vehicles.Count(IsWarning);
        var excursionCount = vehicles.Count(v =>
            v.Shipment is not null
            && (v.EngineTemperature > v.Shipment.MaxSafeTempCelsius
                || v.EngineTemperature < v.Shipment.MinSafeTempCelsius));

        return new FleetSummaryDto
        {
            ConfiguredVehicleCount = configuredCount,
            ActiveVehicleCount = vehicles.Count,
            WarningCount = warningCount,
            AverageFuelLevel = Math.Round(vehicles.Average(v => v.FuelLevel), 1),
            AverageEngineTemperature = Math.Round(vehicles.Average(v => v.EngineTemperature), 1),
            ExcursionCount = excursionCount
        };
    }

    private static bool IsWarning(TelemetryDto telemetry)
    {
        if (telemetry.Shipment is not null)
        {
            return telemetry.EngineTemperature > telemetry.Shipment.MaxSafeTempCelsius
                   || telemetry.EngineTemperature < telemetry.Shipment.MinSafeTempCelsius
                   || telemetry.FuelLevel < 30;
        }

        return telemetry.FuelLevel < 30 || telemetry.EngineTemperature > 100;
    }
}
