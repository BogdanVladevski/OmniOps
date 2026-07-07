using MediatR;
using OmniOps.Application.Commands;
using OmniOps.Application.Dtos;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

public class BootstrapDemoCommandHandler : IRequestHandler<BootstrapDemoCommand, DemoBootstrapDto>
{
    private readonly ITelemetrySimulatorService _simulator;

    public BootstrapDemoCommandHandler(ITelemetrySimulatorService simulator) => _simulator = simulator;

    public async Task<DemoBootstrapDto> Handle(BootstrapDemoCommand request, CancellationToken cancellationToken)
    {
        var result = await _simulator.BootstrapDemoAsync(request.PacketsPerVehicle, cancellationToken);
        return new DemoBootstrapDto(
            result.Vehicles.Count,
            result.TotalPacketsSent,
            result.Message);
    }
}
