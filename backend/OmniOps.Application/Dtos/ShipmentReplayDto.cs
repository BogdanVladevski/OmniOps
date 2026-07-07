namespace OmniOps.Application.Dtos;

public class ShipmentReplayDto
{
    public Guid ShipmentId { get; set; }
    public string VehicleId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public double MinSafeTempCelsius { get; set; }
    public double MaxSafeTempCelsius { get; set; }
    public decimal ValueAtRiskUsd { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public IReadOnlyList<TelemetryDto> Points { get; set; } = Array.Empty<TelemetryDto>();
}
