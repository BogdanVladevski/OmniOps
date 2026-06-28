using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniOps.Core.Entities
{
    public class VehicleTelemetry
    {
        public Guid Id { get; set; }
        public string VehicleId { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; }
        public double FuelLevel { get; set; }
        public double EngineTemperature { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
