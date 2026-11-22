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

/// <summary>A message that may only be sent by callers satisfying <see cref="Policy"/>.</summary>
public interface IRequirePolicy
{
    string Policy { get; }
}

/// <summary>A command whose writes must commit or fail together (e.g. allocating an invoice number).</summary>
public interface ITransactional;
