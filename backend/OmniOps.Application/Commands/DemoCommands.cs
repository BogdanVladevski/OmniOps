using MediatR;
using OmniOps.Application.Dtos;

namespace OmniOps.Application.Commands;

public record BootstrapDemoCommand(int PacketsPerVehicle = 8) : IRequest<DemoBootstrapDto>;
