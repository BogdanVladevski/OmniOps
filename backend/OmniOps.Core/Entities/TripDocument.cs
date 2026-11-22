using OmniOps.Core.Enums;

namespace OmniOps.Core.Entities;

/// <summary>A file attached to a delivery job (delivery photo today; CMR, invoice later).</summary>
public class TripDocument
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid TripId { get; set; }
    public TripDocumentKind Kind { get; set; }

    /// <summary>Key in the file store; never exposed to clients.</summary>
    public string StorageKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CapturedAtUtc { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>For temperature photos: the reading in °C the driver entered from the display.</summary>
    public double? TemperatureCelsius { get; set; }
    public Trip Trip { get; set; } = null!;
}
