using Microsoft.EntityFrameworkCore;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Services;

public class ShipmentRepository : IShipmentRepository
{
    private readonly AppDbContext _context;

    public ShipmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Shipment?> GetActiveShipmentForVehicleAsync(
        string vehicleId,
        CancellationToken cancellationToken = default)
    {
        return _context.Shipments
            .Where(s => s.VehicleId == vehicleId && s.Status == ShipmentStatus.InTransit)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
