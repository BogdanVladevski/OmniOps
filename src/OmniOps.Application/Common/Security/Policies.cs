namespace OmniOps.Application.Common.Security;

/// <summary>What a signed-in person may do follows from their role in the company.</summary>
public static class Policies
{
    /// <summary>Company settings, team, invoice settings: owners only.</summary>
    public const string Owner = "Owner";

    /// <summary>Jobs, fleet, invoices: owners and dispatchers.</summary>
    public const string Office = "Office";

    /// <summary>Work on a job, from the office or the driver's phone (drivers only see their own jobs).</summary>
    public const string JobWork = "JobWork";
}

/// <summary>
/// The message may only be sent by callers satisfying <paramref name="policy"/>. A class attribute rather than a property
/// so it never ends up in request bodies or the API contract.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresPolicyAttribute(string policy) : Attribute
{
    public string Policy { get; } = policy;
}

/// <summary>
/// Needs no signed-in caller, on purpose: signing in, links from emails, public pages, and background work. Handlers of
/// such messages establish whose data they touch themselves.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AnonymousAttribute : Attribute;

/// <summary>A command whose writes must commit or fail together (e.g. allocating an invoice number).</summary>
public interface ITransactional;
