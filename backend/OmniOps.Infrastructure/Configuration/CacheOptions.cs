namespace OmniOps.Infrastructure.Configuration;

public class CacheOptions
{
    public const string SectionName = "Cache";

    public int SlidingExpirationMinutes { get; set; } = 60;
    public int AbsoluteExpirationHours { get; set; } = 24;
}
