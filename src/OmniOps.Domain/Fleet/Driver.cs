namespace OmniOps.Domain.Fleet;

/// <summary>A driver. Signs in on their own phone with a one-time code (see <c>DriverPairingCode</c>).</summary>
public class Driver
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string FullName { get; set; } = string.Empty;

    /// <summary>For the office's "Call driver" (e.g. "+389 70 123 456").</summary>
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
