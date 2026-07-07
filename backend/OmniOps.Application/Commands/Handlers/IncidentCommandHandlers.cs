using MediatR;
using OmniOps.Application.Commands;
using OmniOps.Application.Dtos;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

public class ResolveIncidentCommandHandler : IRequestHandler<ResolveIncidentCommand, IncidentDto>
{
    private readonly IIncidentRepository _incidents;
    private readonly IAuditService _audit;

    public ResolveIncidentCommandHandler(IIncidentRepository incidents, IAuditService audit)
    {
        _incidents = incidents;
        _audit = audit;
    }

    public async Task<IncidentDto> Handle(ResolveIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Incident {request.IncidentId} not found.");
        incident.Status = IncidentStatus.Resolved;
        incident.ResolvedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            await _incidents.AddNoteAsync(new IncidentNote
            {
                Id = Guid.NewGuid(),
                IncidentId = incident.Id,
                Author = "operator",
                Text = request.Notes,
                CreatedAtUtc = DateTime.UtcNow
            }, cancellationToken);
        }
        await _incidents.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("Resolve", "Incident", incident.Id.ToString(), null, request.Notes, cancellationToken);
        return IncidentDto.FromEntity(incident);
    }
}

public class AddIncidentNoteCommandHandler : IRequestHandler<AddIncidentNoteCommand, IncidentNoteDto>
{
    private readonly IIncidentRepository _incidents;

    public AddIncidentNoteCommandHandler(IIncidentRepository incidents) => _incidents = incidents;

    public async Task<IncidentNoteDto> Handle(AddIncidentNoteCommand request, CancellationToken cancellationToken)
    {
        _ = await _incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Incident {request.IncidentId} not found.");
        var note = new IncidentNote
        {
            Id = Guid.NewGuid(),
            IncidentId = request.IncidentId,
            Author = request.Author ?? "operator",
            Text = request.Text,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _incidents.AddNoteAsync(note, cancellationToken);
        await _incidents.SaveChangesAsync(cancellationToken);
        return new IncidentNoteDto(note.Id, note.Author, note.Text, note.CreatedAtUtc);
    }
}

public class AssignIncidentCommandHandler : IRequestHandler<AssignIncidentCommand, IncidentDto>
{
    private readonly IIncidentRepository _incidents;
    private readonly IAuditService _audit;

    public AssignIncidentCommandHandler(IIncidentRepository incidents, IAuditService audit)
    {
        _incidents = incidents;
        _audit = audit;
    }

    public async Task<IncidentDto> Handle(AssignIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = await _incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Incident {request.IncidentId} not found.");
        incident.AssignedTo = request.AssignedTo;
        incident.Status = IncidentStatus.Assigned;
        await _incidents.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("Assign", "Incident", incident.Id.ToString(), request.AssignedTo, null, cancellationToken);
        return IncidentDto.FromEntity(incident);
    }
}
