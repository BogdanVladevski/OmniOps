namespace OmniOps.Application.Dtos;

public class ShipmentInfoDto
{
    public Guid ShipmentId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public decimal ValueAtRiskUsd { get; set; }
    public double MinSafeTempCelsius { get; set; }
    public double MaxSafeTempCelsius { get; set; }

    public int ExcursionDurationSeconds { get; set; }
}
