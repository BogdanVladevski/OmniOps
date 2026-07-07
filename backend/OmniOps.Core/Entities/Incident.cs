using OmniOps.Core.Enums;

namespace OmniOps.Core.Entities;

public class Incident
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid FleetId { get; set; }
    public string VehicleId { get; set; } = string.Empty;
    public IncidentType Type { get; set; }
    public IncidentSeverity Severity { get; set; }
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? AssignedTo { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public ICollection<IncidentNote> Notes { get; set; } = new List<IncidentNote>();
}
