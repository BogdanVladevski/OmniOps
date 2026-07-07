using OmniOps.Core.Enums;

namespace OmniOps.Core.Entities;

public class Trip
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public TripStatus Status { get; set; } = TripStatus.Planned;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Driver? Driver { get; set; }
}
