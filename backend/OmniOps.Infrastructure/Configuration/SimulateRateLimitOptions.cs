namespace OmniOps.Infrastructure.Configuration;

public class SimulateRateLimitOptions
{
    public const string SectionName = "SimulateRateLimit";

    public int PermitLimit { get; set; } = 10;
    public int WindowSeconds { get; set; } = 60;
}
