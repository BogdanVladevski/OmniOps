namespace OmniOps.Infrastructure.Configuration;

public class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>Maximum packets waiting for the ingestion worker; producers wait when it is full.</summary>
    public int QueueCapacity { get; set; } = 10_000;

    /// <summary>Bounded in-process retries for transient processing failures before the packet is rejected.</summary>
    public int ProcessingRetryMaxAttempts { get; set; } = 3;

    public int ProcessingRetryDelayMilliseconds { get; set; } = 500;
}
