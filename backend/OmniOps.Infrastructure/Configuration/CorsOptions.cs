namespace OmniOps.Infrastructure.Configuration;

public class CorsOptions
{
    public const string SectionName = "Cors";

    public string AllowedOrigins { get; set; } = string.Empty;

    public string[] GetOriginsArray()
    {
        if (string.IsNullOrWhiteSpace(AllowedOrigins))
        {
            return [];
        }

        return AllowedOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
