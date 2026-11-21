using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

public class IncidentDetectionService : IIncidentDetectionService
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(5);
    private const double OverspeedKmh = 120;
    private const double HarshBrakeDelta = 25;
    private const double HarshAccelDelta = 20;
    private const int LongIdleMinutes = 15;

    private readonly IDistributedCache _cache;
    private readonly IShipmentRepository _shipmentRepository;

    public IncidentDetectionService(IDistributedCache cache, IShipmentRepository shipmentRepository)
    {
        _cache = cache;
        _shipmentRepository = shipmentRepository;
    }

    public async Task<IncidentDetectionResult> DetectAsync(
        VehicleTelemetry telemetry,
        VehicleTenant tenant,
        CancellationToken cancellationToken = default)
    {
        var incidents = new List<Incident>();
        var pendingCooldownKeys = new List<string>();
        var stateKey = $"incident:state:{tenant.FleetId}:{telemetry.VehicleId}";
        var prevJson = await _cache.GetStringAsync(stateKey, cancellationToken);
        TelemetryState? prev = prevJson is not null
            ? JsonSerializer.Deserialize<TelemetryState>(prevJson)
            : null;

        if (telemetry.Speed > OverspeedKmh)
        {
            await TryAddAsync(telemetry, tenant, IncidentType.Overspeed, IncidentSeverity.Medium,
                $"Overspeed on {telemetry.VehicleId}",
                $"Vehicle exceeded {OverspeedKmh} km/h (current {telemetry.Speed:F0} km/h).",
                incidents, pendingCooldownKeys, cancellationToken);
        }

        if (prev is not null)
        {
            var speedDelta = telemetry.Speed - prev.Speed;
            if (speedDelta < -HarshBrakeDelta)
            {
                await TryAddAsync(telemetry, tenant, IncidentType.HarshBraking, IncidentSeverity.High,
                    $"Harsh braking — {telemetry.VehicleId}",
                    $"Speed dropped {Math.Abs(speedDelta):F0} km/h between readings.",
                    incidents, pendingCooldownKeys, cancellationToken);
            }
            else if (speedDelta > HarshAccelDelta)
            {
                await TryAddAsync(telemetry, tenant, IncidentType.HarshAcceleration, IncidentSeverity.Medium,
                    $"Harsh acceleration — {telemetry.VehicleId}",
                    $"Speed increased {speedDelta:F0} km/h between readings.",
                    incidents, pendingCooldownKeys, cancellationToken);
            }

            if (telemetry.Speed < 2 && prev.Speed < 2)
            {
                var idleMinutes = (telemetry.Timestamp - prev.Timestamp).TotalMinutes + prev.IdleMinutes;
                if (idleMinutes >= LongIdleMinutes)
                {
                    await TryAddAsync(telemetry, tenant, IncidentType.LongIdle, IncidentSeverity.Low,
                        $"Long idle — {telemetry.VehicleId}",
                        $"Vehicle idle for approximately {idleMinutes:F0} minutes.",
                        incidents, pendingCooldownKeys, cancellationToken);
                }
                prev = prev with { IdleMinutes = idleMinutes };
            }
            else
            {
                prev = prev with { IdleMinutes = 0 };
            }
        }

        if (telemetry.BatteryLevel is < 15)
        {
            await TryAddAsync(telemetry, tenant, IncidentType.SensorFailure, IncidentSeverity.High,
                $"Low battery sensor — {telemetry.VehicleId}",
                $"Battery level at {telemetry.BatteryLevel:F0}%.",
                incidents, pendingCooldownKeys, cancellationToken);
        }

        // Quarantined packets have no trustworthy owner, so no shipment safe range applies;
        // the anomaly service still runs its generic checks for them.
        var shipment = tenant.IsUnattributed
            ? null
            : await _shipmentRepository.GetActiveShipmentForVehicleAsync(
                tenant.FleetId, telemetry.VehicleId, cancellationToken);
        if (shipment is not null)
        {
            var temp = telemetry.EngineTemperature;
            if (temp < shipment.MinSafeTempCelsius || temp > shipment.MaxSafeTempCelsius)
            {
                await TryAddAsync(telemetry, tenant, IncidentType.TemperatureExcursion, IncidentSeverity.Critical,
                    $"Temperature excursion — {telemetry.VehicleId}",
                    $"Cargo temp {temp:F1}°C outside safe range {shipment.MinSafeTempCelsius}–{shipment.MaxSafeTempCelsius}°C.",
                    incidents, pendingCooldownKeys, cancellationToken);
            }
        }

        var nextStateJson = JsonSerializer.Serialize(new TelemetryState
        {
            Speed = telemetry.Speed,
            Latitude = telemetry.Latitude,
            Longitude = telemetry.Longitude,
            Timestamp = telemetry.Timestamp,
            IdleMinutes = prev?.IdleMinutes ?? 0
        });

        // State writes are deferred: if persistence fails and the packet is retried,
        // the retry must see the same cooldowns and previous reading as this attempt did.
        return new IncidentDetectionResult(incidents, async ct =>
        {
            foreach (var cooldownKey in pendingCooldownKeys)
            {
                await _cache.SetStringAsync(cooldownKey, "1", new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = Cooldown
                }, ct);
            }

            await _cache.SetStringAsync(stateKey, nextStateJson, ct);
        });
    }

    private async Task TryAddAsync(
        VehicleTelemetry telemetry, VehicleTenant tenant, IncidentType type, IncidentSeverity severity,
        string title, string description, List<Incident> incidents, List<string> pendingCooldownKeys,
        CancellationToken cancellationToken)
    {
        var cooldownKey = $"incident:cooldown:{tenant.FleetId}:{telemetry.VehicleId}:{type}";
        if (await _cache.GetStringAsync(cooldownKey, cancellationToken) is not null)
        {
            return;
        }

        incidents.Add(Create(telemetry, tenant, type, severity, title, description));
        pendingCooldownKeys.Add(cooldownKey);
    }

    private static Incident Create(
        VehicleTelemetry telemetry, VehicleTenant tenant, IncidentType type, IncidentSeverity severity, string title, string description) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = tenant.OrganizationId,
            FleetId = tenant.FleetId,
            VehicleId = telemetry.VehicleId,
            Type = type,
            Severity = severity,
            Status = IncidentStatus.Open,
            Title = title,
            Description = description,
            DetectedAtUtc = telemetry.Timestamp,
            Latitude = telemetry.Latitude,
            Longitude = telemetry.Longitude
        };

    private record TelemetryState
    {
        public double Speed { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public DateTime Timestamp { get; init; }
        public double IdleMinutes { get; init; }
    }
}
