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
    public double EngineTemperature { get; set; }
    public DateTime Timestamp { get; set; }

    public static TelemetryDto FromEntity(VehicleTelemetry entity)
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
            EngineTemperature = entity.EngineTemperature,
            Timestamp = entity.Timestamp
        };
    }
}
