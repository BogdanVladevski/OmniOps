using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MediatR;
using OmniOps.Core.Telemetry;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

namespace OmniOps.Infrastructure.Handlers;

public class ProcessTelemetryCommandHandler : IRequestHandler<ProcessTelemetryCommand, bool>
{
    private readonly AppDbContext _context;
    private readonly ITelemetryCacheService _cacheService;

    public ProcessTelemetryCommandHandler(AppDbContext context, ITelemetryCacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<bool> Handle(ProcessTelemetryCommand request, CancellationToken cancellationToken)
    {
        request.Telemetry.Id = Guid.NewGuid();

        await _cacheService.SetLatestTelemetryAsync(request.Telemetry);

        await _context.Telemetries.AddAsync(request.Telemetry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}

