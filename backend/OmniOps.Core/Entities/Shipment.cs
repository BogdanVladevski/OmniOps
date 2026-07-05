using OmniOps.Core.Enums;

namespace OmniOps.Core.Entities;

public class Shipment
{
    public Guid Id { get; set; }
    public string VehicleId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;

    /// <summary>Minimum safe cargo temperature in °C. Below this triggers a cold excursion.</summary>
    public double MinSafeTempCelsius { get; set; }

    /// <summary>Maximum safe cargo temperature in °C. Above this triggers a warm excursion.</summary>
    public double MaxSafeTempCelsius { get; set; }

    public decimal ValueAtRiskUsd { get; set; }
    public DateTime DepartedAtUtc { get; set; }
    public DateTime ExpectedDeliveryUtc { get; set; }
    public ShipmentStatus Status { get; set; }
}
