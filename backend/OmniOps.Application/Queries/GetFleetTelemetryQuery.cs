using MediatR;
using OmniOps.Application.Dtos;

namespace OmniOps.Application.Queries;

public record GetFleetTelemetryQuery : IRequest<FleetTelemetryDto>;
