using MediatR;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;

namespace OmniOps.Application.Commands;

/// <param name="Telemetry">The packet.</param>
/// <param name="KnownTenant">
/// Owner when the caller already knows it (e.g. a driver reporting against their job). Null means resolve it
/// from the vehicle's external id, which quarantines ids shared by several fleets.
/// </param>
public record ProcessTelemetryCommand(VehicleTelemetry Telemetry, VehicleTenant? KnownTenant = null) : IRequest<bool>;
