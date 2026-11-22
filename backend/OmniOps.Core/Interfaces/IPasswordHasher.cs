namespace OmniOps.Core.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>Constant-time check. Also true when the hash should be upgraded (<paramref name="needsRehash"/>).</summary>
    bool Verify(string password, string hash, out bool needsRehash);
}
