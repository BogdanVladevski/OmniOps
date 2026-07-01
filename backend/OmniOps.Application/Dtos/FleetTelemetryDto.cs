namespace OmniOps.Application.Dtos;

public class FleetTelemetryDto
{
    public IReadOnlyList<TelemetryDto> Vehicles { get; init; } = [];

    public FleetSummaryDto Summary { get; init; } = new();
}

public class FleetSummaryDto
{
    public int ConfiguredVehicleCount { get; init; }

    public int ActiveVehicleCount { get; init; }

    public int WarningCount { get; init; }

    public double? AverageFuelLevel { get; init; }

    public double? AverageEngineTemperature { get; init; }
}
