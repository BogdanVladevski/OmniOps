using System.ComponentModel.DataAnnotations;

namespace OmniOps.Api.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "omniops";
    public string Audience { get; set; } = "omniops";

    /// <summary>HMAC signing key. Required; at least 32 characters. Never commit it: use user secrets or the environment.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Access tokens are short-lived; the app renews them with a refresh token.</summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 30;
}
