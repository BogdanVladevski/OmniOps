using System.Threading;
using System.Threading.Tasks;
using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces
{
    public interface IAnomalyDetectionService
    {
        Task<bool> AnalyzeTelemetryAsync(VehicleTelemetry telemetry, CancellationToken cancellationToken = default);
    }
}
