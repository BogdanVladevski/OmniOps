using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

public class CorrelationContext : ICorrelationContext
{
    private static readonly AsyncLocal<string?> Current = new();

    public string CorrelationId => Current.Value ?? Guid.NewGuid().ToString("N");

    public void SetCorrelationId(string correlationId) =>
        Current.Value = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId;
}
