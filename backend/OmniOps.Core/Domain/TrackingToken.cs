using System.Security.Cryptography;

namespace OmniOps.Core.Domain;

/// <summary>Unguessable, URL-safe tokens for public tracking links (128 bits of randomness).</summary>
public static class TrackingToken
{
    public static string Create()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool LooksValid(string? token) =>
        token is { Length: >= 16 and <= 64 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
