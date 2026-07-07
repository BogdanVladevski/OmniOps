using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class DeviceRegistrationRepository : IDeviceRegistrationRepository
{
    private readonly AppDbContext _context;

    public DeviceRegistrationRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<DeviceRegistration>> GetByUserAsync(string userId, CancellationToken cancellationToken = default) =>
        await _context.DeviceRegistrations.AsNoTracking()
            .Where(d => d.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task UpsertAsync(DeviceRegistration registration, CancellationToken cancellationToken = default)
    {
        var existing = await _context.DeviceRegistrations.FirstOrDefaultAsync(
            d => d.UserId == registration.UserId && d.PushToken == registration.PushToken, cancellationToken);
        if (existing is null)
            await _context.DeviceRegistrations.AddAsync(registration, cancellationToken);
        else
            existing.LastSeenAtUtc = DateTime.UtcNow;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
