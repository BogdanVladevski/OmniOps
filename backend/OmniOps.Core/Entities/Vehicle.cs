using OmniOps.Core.Enums;

namespace OmniOps.Core.Entities;

public class Vehicle
{
    public Guid Id { get; set; }
    public Guid FleetId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string? Vin { get; set; }
    public string? Registration { get; set; }
    public string? InsuranceProvider { get; set; }
    public DateTime? InsuranceExpiryUtc { get; set; }
    public VehicleOperationalStatus Status { get; set; } = VehicleOperationalStatus.Active;
    public Guid? AssignedDriverId { get; set; }
    public Fleet Fleet { get; set; } = null!;
    public Driver? AssignedDriver { get; set; }
    public ICollection<VehicleMaintenanceRecord> MaintenanceHistory { get; set; } = new List<VehicleMaintenanceRecord>();
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
