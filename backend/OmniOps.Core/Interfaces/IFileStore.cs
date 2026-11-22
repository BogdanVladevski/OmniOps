namespace OmniOps.Core.Interfaces;

/// <summary>Stores uploaded files (delivery photos, documents) under server-generated keys.</summary>
public interface IFileStore
{
    Task SaveAsync(string key, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Opens the file for reading, or null if it does not exist.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);
}
