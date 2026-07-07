namespace OmniOps.Core.Entities;

public class Fleet
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<Driver> Drivers { get; set; } = new List<Driver>();
    public ICollection<Depot> Depots { get; set; } = new List<Depot>();
}
