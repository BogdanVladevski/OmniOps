using OmniOps.Core.Entities;
using OmniOps.Core.Enums;

namespace OmniOps.Core.Interfaces;

public interface IIncidentRepository
{
    Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Incident>> GetByFleetAsync(
        Guid fleetId, IncidentStatus? status = null, CancellationToken cancellationToken = default);
    Task AddAsync(Incident incident, CancellationToken cancellationToken = default);
    Task AddNoteAsync(IncidentNote note, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
