using System;
using OmniOps.Core.Entities;

namespace OmniOps.Core.DTOs
{
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
            if (entity == null) throw new ArgumentNullException(nameof(entity));

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

        public VehicleTelemetry ToEntity()
        {
            return new VehicleTelemetry
            {
                Id = Id,
                VehicleId = VehicleId,
                Latitude = Latitude,
                Longitude = Longitude,
                Speed = Speed,
                FuelLevel = FuelLevel,
                EngineTemperature = EngineTemperature,
                Timestamp = Timestamp
            };
        }
    }
}
