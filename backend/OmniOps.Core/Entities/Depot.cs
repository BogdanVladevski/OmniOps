namespace OmniOps.Core.Entities;

public class Depot
{
    public Guid Id { get; set; }
    public Guid FleetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Capacity { get; set; }
    public Fleet Fleet { get; set; } = null!;
}
