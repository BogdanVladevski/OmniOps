using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetFleetTelemetryQueryHandler : IRequestHandler<GetFleetTelemetryQuery, FleetTelemetryDto>
{
    private readonly ITelemetryCacheService _cacheService;
    private readonly IFleetVehicleRegistry _fleetRegistry;

    public GetFleetTelemetryQueryHandler(
        ITelemetryCacheService cacheService,
        IFleetVehicleRegistry fleetRegistry)
    {
        _cacheService = cacheService;
        _fleetRegistry = fleetRegistry;
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
            if (telemetry is not null)
            {
                vehicles.Add(TelemetryDto.FromEntity(telemetry));
            }
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

        return new FleetSummaryDto
        {
            ConfiguredVehicleCount = configuredCount,
            ActiveVehicleCount = vehicles.Count,
            WarningCount = warningCount,
            AverageFuelLevel = Math.Round(vehicles.Average(v => v.FuelLevel), 1),
            AverageEngineTemperature = Math.Round(vehicles.Average(v => v.EngineTemperature), 1)
        };
    }

    private static bool IsWarning(TelemetryDto telemetry) =>
        telemetry.FuelLevel < 30 || telemetry.EngineTemperature > 100;
}
