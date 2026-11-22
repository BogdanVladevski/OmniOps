namespace OmniOps.Api.Authentication;

/// <summary>Claim names in OmniOps access tokens (kept short; mapped as-is, no legacy SOAP names).</summary>
public static class OmniClaims
{
    public const string Subject = "sub";
    public const string Organization = "org";
    public const string Role = "role";
    public const string Driver = "driver";
    public const string Name = "name";

    /// <summary>Issue time in milliseconds: ending sessions must also end tokens issued earlier in the same second.</summary>
    public const string IssuedAtMs = "iat_ms";
}
