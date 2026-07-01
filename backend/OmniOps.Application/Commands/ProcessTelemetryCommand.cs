using MediatR;
using OmniOps.Core.Entities;

namespace OmniOps.Application.Commands;

public record ProcessTelemetryCommand(VehicleTelemetry Telemetry) : IRequest<bool>;
