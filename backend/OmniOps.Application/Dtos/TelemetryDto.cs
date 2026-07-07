using OmniOps.Core.Entities;

namespace OmniOps.Application.Dtos;

public class TelemetryDto
{
    public Guid Id { get; set; }
    public string VehicleId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Speed { get; set; }
    public double FuelLevel { get; set; }
    public double? Heading { get; set; }
    public double? BatteryLevel { get; set; }

    /// <summary>Cargo temperature in °C (sourced from EngineTemperature on the telemetry packet).</summary>
    public double EngineTemperature { get; set; }

    public DateTime Timestamp { get; set; }

    /// <summary>Active cold-chain shipment on this vehicle, null if no active shipment.</summary>
    public ShipmentInfoDto? Shipment { get; set; }

    public static TelemetryDto FromEntity(VehicleTelemetry entity, ShipmentInfoDto? shipment = null)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new TelemetryDto
        {
            Id = entity.Id,
            VehicleId = entity.VehicleId,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            Speed = entity.Speed,
            FuelLevel = entity.FuelLevel,
            Heading = entity.Heading,
            BatteryLevel = entity.BatteryLevel,
            EngineTemperature = entity.EngineTemperature,
            Timestamp = entity.Timestamp,
            Shipment = shipment
        };
    }
}
