namespace OmniOps.Core.Entities;

public class IncidentNote
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public string Author { get; set; } = "system";
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public Incident Incident { get; set; } = null!;
}
