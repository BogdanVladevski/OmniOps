using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetFleetTelemetryQueryHandler : IRequestHandler<GetFleetTelemetryQuery, FleetTelemetryDto>
{
    private readonly ITelemetryCacheService _cacheService;
    private readonly IJobRepository _jobs;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ITenantContext _tenant;

    public GetFleetTelemetryQueryHandler(
        ITelemetryCacheService cacheService,
        IJobRepository jobs,
        IShipmentRepository shipmentRepository,
        IVehicleRepository vehicleRepository,
        ITenantContext tenant)
    {
        _cacheService = cacheService;
        _jobs = jobs;
        _shipmentRepository = shipmentRepository;
        _vehicleRepository = vehicleRepository;
        _tenant = tenant;
    }

    public async Task<FleetTelemetryDto> Handle(
        GetFleetTelemetryQuery request,
        CancellationToken cancellationToken)
    {
        // The caller's own trucks (not a server-wide list), each looked up within its fleet.
        var owned = await _jobs.ListVehiclesAsync(_tenant.OrganizationId, cancellationToken);
        var vehicles = new List<TelemetryDto>();

        foreach (var vehicle in owned)
        {
            var telemetry = await _cacheService.GetLatestTelemetryAsync(vehicle.FleetId, vehicle.ExternalId);
            if (telemetry is null)
            {
                continue;
            }

            ShipmentInfoDto? shipmentInfo = null;
            try
            {
                var shipment = await _shipmentRepository.GetActiveShipmentForVehicleAsync(
                    vehicle.FleetId, vehicle.ExternalId, cancellationToken);

                if (shipment is not null)
                {
                    // ExcursionDurationSeconds is not tracked here (the anomaly detection service owns that);
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
            Summary = BuildSummary(owned.Count, vehicles)
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
            AverageFuelLevel = RoundOrNull(vehicles.Average(v => v.FuelLevel)),
            AverageEngineTemperature = RoundOrNull(vehicles.Average(v => v.EngineTemperature)),
            ExcursionCount = excursionCount
        };
    }

    // Average over nullable values ignores missing readings and is null when none were reported.
    private static double? RoundOrNull(double? value) => value is { } v ? Math.Round(v, 1) : null;

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
