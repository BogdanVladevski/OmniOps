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
    Task<VehicleTelemetry?> GetLatestTelemetryAsync(string vehicleId);
}

