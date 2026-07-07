namespace OmniOps.Core.Interfaces;

public record TelemetrySimulateResult(string VehicleId, int PacketsSent);

public record DemoBootstrapResult(
    IReadOnlyList<TelemetrySimulateResult> Vehicles,
    int TotalPacketsSent,
    string Message);

public interface ITelemetrySimulatorService
{
    Task<TelemetrySimulateResult> SimulateAsync(
        string vehicleId,
        int packets,
        CancellationToken cancellationToken = default);

    Task<DemoBootstrapResult> BootstrapDemoAsync(
        int packetsPerVehicle = 8,
        CancellationToken cancellationToken = default);
}
