using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using OmniOps.Core.Interfaces;

namespace OmniOps.Core.Entities
{
    public class VehicleTelemetry : IHasDomainEvents
    {
        public Guid Id { get; set; }
        public string VehicleId { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; }
        public double FuelLevel { get; set; }
        public double? Heading { get; set; }
        public double? BatteryLevel { get; set; }

        /// <summary>Extensible sensor readings as JSON, e.g. {"humidity":42.1,"doorOpen":false}.</summary>
        public string? SensorReadingsJson { get; set; }

        public double EngineTemperature { get; set; }
        public DateTime Timestamp { get; set; }

        private readonly List<IDomainEvent> _domainEvents = new();

        [JsonIgnore]
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        public void AddDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }
    }
}
