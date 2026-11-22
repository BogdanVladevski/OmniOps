using OmniOps.Core.Enums;

namespace OmniOps.Core.Entities;

/// <summary>
/// A delivery job: one truck and driver taking a load to a customer. Drivers report GPS against it,
/// close it with proof of delivery, and the customer follows it through a private tracking link.
/// </summary>
public class Trip
{
    public Guid Id { get; set; }

    /// <summary>Owning organization; every job query filters on it.</summary>
    public Guid OrganizationId { get; set; }

    public Guid FleetId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public TripStatus Status { get; set; } = TripStatus.Planned;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }

    /// <summary>Who the load is for, as shown on the tracking page and the paperwork.</summary>
    public string? CustomerName { get; set; }

    /// <summary>Customer's order / PO / consignment number.</summary>
    public string? Reference { get; set; }

    public double? DestinationLatitude { get; set; }
    public double? DestinationLongitude { get; set; }
    public DateTime? PlannedDeliveryUtc { get; set; }

    /// <summary>Unguessable token for the public tracking link; null disables the link.</summary>
    public string? TrackingToken { get; set; }

    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public double? LastSpeedKmh { get; set; }
    public DateTime? LastPositionAtUtc { get; set; }

    /// <summary>Name of the person who received the goods.</summary>
    public string? RecipientName { get; set; }

    public string? DeliveryNote { get; set; }
    public double? DeliveredLatitude { get; set; }
    public double? DeliveredLongitude { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
    public Driver? Driver { get; set; }
    public ICollection<TripDocument> Documents { get; set; } = new List<TripDocument>();
}
