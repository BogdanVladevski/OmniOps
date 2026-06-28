using MediatR;
using OmniOps.Core.Entities;

namespace OmniOps.Core.Telemetry;

public record GetLatestTelemetryQuery(string VehicleId) : IRequest<VehicleTelemetry?>;