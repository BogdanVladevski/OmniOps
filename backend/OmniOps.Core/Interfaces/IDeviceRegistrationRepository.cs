using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IDeviceRegistrationRepository
{
    Task<IReadOnlyList<DeviceRegistration>> GetByUserAsync(string userId, CancellationToken cancellationToken = default);
    Task UpsertAsync(DeviceRegistration registration, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
