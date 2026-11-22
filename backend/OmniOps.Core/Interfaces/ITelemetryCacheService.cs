using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface ITelemetryCacheService
{
    Task SetLatestTelemetryAsync(VehicleTelemetry telemetry);
    /// <summary>Latest packet for a vehicle in a fleet (external ids repeat across fleets).</summary>
    Task<VehicleTelemetry?> GetLatestTelemetryAsync(Guid fleetId, string vehicleId);
}

