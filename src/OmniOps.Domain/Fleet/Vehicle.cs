namespace OmniOps.Domain.Fleet;

/// <summary>A truck. Its name is what drivers and the office call it ("Truck-1", or the plate).</summary>
public class Vehicle
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    /// <summary>Unique within the organization.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Registration { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
