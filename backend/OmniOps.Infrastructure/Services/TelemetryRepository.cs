using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class TelemetryRepository : ITelemetryRepository
{
    private readonly AppDbContext _context;

    public TelemetryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(VehicleTelemetry telemetry, CancellationToken cancellationToken = default)
    {
        await _context.Telemetries.AddAsync(telemetry, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
