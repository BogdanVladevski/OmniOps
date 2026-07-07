using MediatR;
using OmniOps.Application.Dtos;

namespace OmniOps.Application.Queries;

public record GetShipmentReplayQuery(
    Guid ShipmentId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    DateTime? AnchorUtc) : IRequest<ShipmentReplayDto?>;
