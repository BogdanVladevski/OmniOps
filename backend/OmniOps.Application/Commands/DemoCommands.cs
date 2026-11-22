using MediatR;
using OmniOps.Application.Dtos;

namespace OmniOps.Application.Commands;

/// <summary>Loads (or refreshes) a demo delivery day. The packet count is ignored; kept for older clients.</summary>
public record BootstrapDemoCommand(int PacketsPerVehicle = 8) : IRequest<DemoBootstrapDto>;
