namespace OmniOps.Core.Enums;

/// <summary>Stored as integers: never renumber existing values.</summary>
public enum TripDocumentKind
{
    /// <summary>Proof of delivery: signed delivery note or the goods at the door.</summary>
    DeliveryPhoto = 0,

    /// <summary>Signed loading note / CMR at pickup.</summary>
    PickupDocument = 1,

    /// <summary>Photo of the fridge unit's display, with the reading the driver entered.</summary>
    TemperaturePhoto = 2,
}
