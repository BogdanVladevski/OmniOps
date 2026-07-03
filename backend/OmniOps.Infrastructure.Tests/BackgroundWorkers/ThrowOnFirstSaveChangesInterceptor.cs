using Microsoft.EntityFrameworkCore.Diagnostics;

namespace OmniOps.Infrastructure.Tests.BackgroundWorkers;

/// <summary>
/// Simulates a transient DB failure on the first SaveChanges call across all scopes.
/// Must be registered after OutboxSaveChangesInterceptor so outbox rows are captured first.
/// </summary>
internal sealed class ThrowOnFirstSaveChangesInterceptor : SaveChangesInterceptor
{
    private static int _globalSaveAttempts;

    public static void ResetForTests() => Interlocked.Exchange(ref _globalSaveAttempts, 0);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _globalSaveAttempts) == 1)
        {
            throw new InvalidOperationException("simulated db failure");
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
