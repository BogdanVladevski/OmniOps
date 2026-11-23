namespace OmniOps.Core.Entities;

public class Driver
{
    public Guid Id { get; set; }
    public Guid FleetId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? LicenseNumber { get; set; }

    /// <summary>For the office's "Call driver" (e.g. "+389 70 123 456").</summary>
    public string? Phone { get; set; }
    public double SafetyScore { get; set; } = 100;
    public Fleet Fleet { get; set; } = null!;
}
