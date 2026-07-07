namespace OmniOps.Core.Entities;

public class VehicleMaintenanceRecord
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime PerformedAtUtc { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
}
