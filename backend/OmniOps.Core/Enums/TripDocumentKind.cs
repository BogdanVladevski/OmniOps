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

    /// <summary>The receiver's signature drawn on the driver's phone; <c>Note</c> holds the name they signed as.</summary>
    Signature = 3,

    /// <summary>Damage, shortage or another problem, with a photo and a short description in <c>Note</c>.</summary>
    Exception = 4,
}
