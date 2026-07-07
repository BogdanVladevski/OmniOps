namespace OmniOps.Core.Entities;

public class Driver
{
    public Guid Id { get; set; }
    public Guid FleetId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? LicenseNumber { get; set; }
    public double SafetyScore { get; set; } = 100;
    public Fleet Fleet { get; set; } = null!;
}
