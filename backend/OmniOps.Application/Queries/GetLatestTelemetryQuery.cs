using MediatR;
using OmniOps.Application.Dtos;

namespace OmniOps.Application.Queries;

public record GetLatestTelemetryQuery(string VehicleId) : IRequest<TelemetryDto?>;
