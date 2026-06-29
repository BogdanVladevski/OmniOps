using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OmniOps.Infrastructure.Hubs;

using MediatR;
using OmniOps.Core.Telemetry;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;

using Microsoft.AspNetCore.SignalR;

namespace OmniOps.Infrastructure.Handlers;

public class ProcessTelemetryCommandHandler : IRequestHandler<ProcessTelemetryCommand, bool>
{
    private readonly AppDbContext _context;
    private readonly ITelemetryCacheService _cacheService;
    private readonly IHubContext<TelemetryHub> _hubContext;

    public ProcessTelemetryCommandHandler(AppDbContext context, ITelemetryCacheService cacheService, IHubContext<TelemetryHub> hubContext)
    {
        _context = context;
        _cacheService = cacheService;
        _hubContext = hubContext; 
    }

    public async Task<bool> Handle(ProcessTelemetryCommand request, CancellationToken cancellationToken)
    {
        request.Telemetry.Id = Guid.NewGuid();

       
        await _cacheService.SetLatestTelemetryAsync(request.Telemetry);

       
        await _context.Telemetries.AddAsync(request.Telemetry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        
        await _hubContext.Clients.All 
            .SendAsync("ReceiveTelemetryUpdate", request.Telemetry, cancellationToken);

        return true;
    }
}