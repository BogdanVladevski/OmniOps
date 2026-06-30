using MediatR;
using OmniOps.Core.DTOs;

namespace OmniOps.Core.Telemetry;

public record GetLatestTelemetryQuery(string VehicleId) : IRequest<TelemetryDto?>;