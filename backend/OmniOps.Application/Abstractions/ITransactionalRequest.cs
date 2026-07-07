namespace OmniOps.Application.Abstractions;

/// <summary>
/// Marker for MediatR requests that must run inside an explicit database transaction.
/// </summary>
public interface ITransactionalRequest;
