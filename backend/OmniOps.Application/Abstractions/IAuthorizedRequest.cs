namespace OmniOps.Application.Abstractions;

/// <summary>
/// Marker for MediatR requests that require an authorization policy before handler execution.
/// </summary>
public interface IAuthorizedRequest
{
    string RequiredPolicy { get; }
}
