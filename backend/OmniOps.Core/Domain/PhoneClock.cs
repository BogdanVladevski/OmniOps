namespace OmniOps.Core.Domain;

/// <summary>
/// Times reported by a driver's phone. Actions taken without signal are sent later, so the moment they
/// happened comes from the phone — but a phone clock can be wrong, so it is kept within sensible bounds.
/// </summary>
public static class PhoneClock
{
    /// <summary>Phone clocks run a little fast; anything further ahead than this is treated as "now".</summary>
    public static readonly TimeSpan AllowedAhead = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The reported time, or <paramref name="nowUtc"/> when none was sent or it is in the future; never earlier
    /// than <paramref name="notBeforeUtc"/> (e.g. when the job was created or started).
    /// </summary>
    public static DateTime Resolve(DateTimeOffset? reported, DateTime notBeforeUtc, DateTime nowUtc)
    {
        if (reported is not { } r)
        {
            return nowUtc;
        }

        var at = r.UtcDateTime;
        if (at > nowUtc + AllowedAhead)
        {
            return nowUtc;
        }

        if (at > nowUtc)
        {
            at = nowUtc;
        }

        return at < notBeforeUtc ? notBeforeUtc : at;
    }
}
