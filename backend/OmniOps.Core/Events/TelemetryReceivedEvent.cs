using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Core.Events;

public class TelemetryReceivedEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public VehicleTelemetry Telemetry { get; }

    public TelemetryReceivedEvent(VehicleTelemetry telemetry)
    {
        Telemetry = telemetry;
    }
}
