using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

public partial class TelemetrySimulatorService : ITelemetrySimulatorService
{
    private static readonly ActivitySource SimulatorSource = new("OmniOps.Api.Simulator");

    private readonly ITelemetryIngestionQueue _queue;
    private readonly IFleetVehicleRegistry _vehicleRegistry;
    private readonly ITelemetryMetrics _metrics;
    private readonly ILogger<TelemetrySimulatorService> _logger;

    public TelemetrySimulatorService(
        ITelemetryIngestionQueue queue,
        IFleetVehicleRegistry vehicleRegistry,
        ITelemetryMetrics metrics,
        ILogger<TelemetrySimulatorService> logger)
    {
        _queue = queue;
        _vehicleRegistry = vehicleRegistry;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<TelemetrySimulateResult> SimulateAsync(
        string vehicleId,
        int packets,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidVehicleId(vehicleId))
        {
            throw new ArgumentException("Vehicle ID must be a non-empty alphanumeric string.", nameof(vehicleId));
        }

        if (packets is <= 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(packets), "Packets count must be between 1 and 100.");
        }

        using var activity = SimulatorSource.StartActivity("SimulateTelemetryStream");
        activity?.SetTag("vehicle.id", vehicleId);
        activity?.SetTag("packets.count", packets);

        var random = new Random();
        var successfullySent = 0;

        for (var i = 0; i < packets; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mockTelemetry = new VehicleTelemetry
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicleId,
                Latitude = 41.9981 + (random.NextDouble() - 0.5) * 0.05,
                Longitude = 21.4254 + (random.NextDouble() - 0.5) * 0.05,
                Speed = random.Next(50, 120),
                FuelLevel = Math.Round(80.0 - (i * 0.5), 2),
                Heading = random.Next(0, 360),
                BatteryLevel = Math.Round(60.0 + random.NextDouble() * 35, 1),
                EngineTemperature = random.Next(85, 105),
                Timestamp = DateTime.UtcNow
            };

            try
            {
                await _queue.EnqueueAsync(mockTelemetry, cancellationToken);
                successfullySent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Simulator failed to queue telemetry for vehicle {VehicleId}", vehicleId);
            }

            await Task.Delay(50, cancellationToken);
        }

        _metrics.RecordSimulatePacketsPublished(successfullySent);
        return new TelemetrySimulateResult(vehicleId, successfullySent);
    }

    public async Task<DemoBootstrapResult> BootstrapDemoAsync(
        int packetsPerVehicle = 8,
        CancellationToken cancellationToken = default)
    {
        var vehicleIds = _vehicleRegistry.GetConfiguredVehicleIds();
        var results = new List<TelemetrySimulateResult>();

        foreach (var vehicleId in vehicleIds)
        {
            var result = await SimulateAsync(vehicleId, packetsPerVehicle, cancellationToken);
            results.Add(result);
        }

        var total = results.Sum(r => r.PacketsSent);
        return new DemoBootstrapResult(
            results,
            total,
            $"Queued {total} demo telemetry packets across {results.Count} vehicles.");
    }

    private static bool IsValidVehicleId(string vehicleId) =>
        !string.IsNullOrWhiteSpace(vehicleId) && VehicleIdPattern().IsMatch(vehicleId);

    [GeneratedRegex("^[a-zA-Z0-9_-]+$")]
    private static partial Regex VehicleIdPattern();
}
