namespace OmniOps.Domain.Jobs;

/// <summary>How an arrival at a stop was recorded. Stored as integers: never renumber.</summary>
public enum ArrivalSource
{
    /// <summary>The phone's GPS entered the stop's area.</summary>
    Gps = 0,

    /// <summary>The driver tapped "I've arrived" (the phone's position at that moment is kept).</summary>
    Driver = 1,
}
