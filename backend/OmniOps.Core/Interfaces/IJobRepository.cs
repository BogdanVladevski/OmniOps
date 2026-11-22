using OmniOps.Core.Entities;
using OmniOps.Core.Enums;

namespace OmniOps.Core.Interfaces;

/// <summary>
/// Delivery jobs (<see cref="Trip"/>) and their documents. Every method that takes an organization id
/// returns nothing for other organizations' data — callers turn that into 404.
/// </summary>
public interface IJobRepository
{
    /// <summary>Tracked job with vehicle, driver and documents, or null if it is not in this organization.</summary>
    Task<Trip?> GetAsync(Guid jobId, Guid organizationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Trip>> ListAsync(
        Guid organizationId,
        IReadOnlyCollection<TripStatus> statuses,
        Guid? driverId,
        DateTime? completedSinceUtc,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Untracked job for a public tracking token, regardless of organization.</summary>
    Task<Trip?> GetByTrackingTokenAsync(string trackingToken, CancellationToken cancellationToken = default);

    Task<Vehicle?> GetVehicleAsync(Guid vehicleId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<Driver?> GetDriverAsync(Guid driverId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vehicle>> ListVehiclesAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Driver>> ListDriversAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>All organizations: jobs where a truck may be waiting now (arrived, not yet released). For the alert worker.</summary>
    Task<IReadOnlyList<Trip>> ListWaitingAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Trip job, CancellationToken cancellationToken = default);
    Task AddDocumentAsync(TripDocument document, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
