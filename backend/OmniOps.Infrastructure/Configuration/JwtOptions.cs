namespace OmniOps.Infrastructure.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "OmniOps";
    public string Audience { get; set; } = "OmniOps.Clients";
    public int ExpirationMinutes { get; set; } = 60;
    public bool RequireAuthentication { get; set; }

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(Secret) && Secret.Length >= 32;
}
