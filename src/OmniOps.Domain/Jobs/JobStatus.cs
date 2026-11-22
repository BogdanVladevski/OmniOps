namespace OmniOps.Domain.Jobs;

/// <summary>Stored as integers: never renumber.</summary>
public enum JobStatus
{
    Planned = 0,
    InProgress = 1,
    Delivered = 2,
    Cancelled = 3,
}
