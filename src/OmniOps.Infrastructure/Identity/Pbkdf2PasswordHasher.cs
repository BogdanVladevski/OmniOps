using System.Security.Cryptography;
using OmniOps.Application.Common.Interfaces;

namespace OmniOps.Infrastructure.Identity;

/// <summary>
/// PBKDF2-HMAC-SHA256 with a random 16-byte salt (OWASP 2023: 600,000 iterations).
/// Stored as "pbkdf2-sha256$iterations$salt$hash" so the cost can be raised later.
/// </summary>
public sealed class Pbkdf2PasswordHasher(int targetIterations = 600_000) : IPasswordHasher
{
    private const string Scheme = "pbkdf2-sha256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;


    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, targetIterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Scheme}${targetIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash, out bool needsRehash)
    {
        needsRehash = false;
        var parts = hash.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        var ok = CryptographicOperations.FixedTimeEquals(actual, expected);
        needsRehash = ok && iterations < targetIterations;
        return ok;
    }
}
