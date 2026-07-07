using OmniOps.Core.Enums;

namespace OmniOps.Application.Dtos;

public record IncidentDto(
    Guid Id,
    Guid FleetId,
    string VehicleId,
    string Type,
    string Severity,
    string Status,
    string Title,
    string Description,
    DateTime DetectedAtUtc,
    DateTime? ResolvedAtUtc,
    string? AssignedTo)
{
    public static IncidentDto FromEntity(Core.Entities.Incident i) => new(
        i.Id, i.FleetId, i.VehicleId, i.Type.ToString(), i.Severity.ToString(),
        i.Status.ToString(), i.Title, i.Description, i.DetectedAtUtc, i.ResolvedAtUtc, i.AssignedTo);
}

public record IncidentNoteDto(Guid Id, string Author, string Text, DateTime CreatedAtUtc);
