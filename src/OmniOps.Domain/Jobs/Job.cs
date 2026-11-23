using OmniOps.Domain.Fleet;

namespace OmniOps.Domain.Jobs;

/// <summary>
/// A delivery job: one truck and driver taking a load to a customer. Drivers report GPS against it,
/// close it with proof of delivery, and the customer follows it through a private tracking link.
/// </summary>
public class Job
{
    public Guid Id { get; set; }

    /// <summary>Owning organization; every job query filters on it.</summary>
    public Guid OrganizationId { get; set; }

    public Guid VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Planned;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }

    /// <summary>Who the load is for, as shown on the tracking page and the paperwork.</summary>
    public string? CustomerName { get; set; }

    /// <summary>The customer record; <see cref="CustomerName"/> keeps the name as it was when the job was created.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Customer's order / PO / consignment number.</summary>
    public string? Reference { get; set; }

    public double? OriginLatitude { get; set; }
    public double? OriginLongitude { get; set; }
    public double? DestinationLatitude { get; set; }
    public double? DestinationLongitude { get; set; }
    public DateTime? PlannedDeliveryUtc { get; set; }

    /// <summary>Safe cargo range for refrigerated loads (both set or both null). Turns on the temperature log.</summary>
    public double? MinTempCelsius { get; set; }

    public double? MaxTempCelsius { get; set; }

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

    /// <summary>Invoice issued for this delivery, if any.</summary>
    public Guid? InvoiceId { get; set; }

    // Waiting time (detention). Pickup: from arrival until the job starts. Delivery: from arrival until delivered.
    public DateTime? PickupArrivedAtUtc { get; set; }
    public ArrivalSource? PickupArrivalSource { get; set; }
    public double? PickupArrivalLatitude { get; set; }
    public double? PickupArrivalLongitude { get; set; }
    public DateTime? DeliveryArrivedAtUtc { get; set; }
    public ArrivalSource? DeliveryArrivalSource { get; set; }
    public double? DeliveryArrivalLatitude { get; set; }
    public double? DeliveryArrivalLongitude { get; set; }

    /// <summary>When the office was told free waiting time ran out (once per stop).</summary>
    public DateTime? PickupWaitAlertedAtUtc { get; set; }
    public DateTime? DeliveryWaitAlertedAtUtc { get; set; }

    /// <summary>Email updates already handled for the customer (sent, or skipped because a later one went out).</summary>
    public DateTime? CustomerPickedUpNoticeAtUtc { get; set; }
    public DateTime? CustomerArrivingNoticeAtUtc { get; set; }
    public DateTime? CustomerDeliveredNoticeAtUtc { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
    public Driver? Driver { get; set; }
    public ICollection<JobDocument> Documents { get; set; } = [];
}
