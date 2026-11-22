namespace OmniOps.Core.Enums;

/// <summary>Stored as integers: never renumber.</summary>
public enum UserRole
{
    /// <summary>Runs the company account: everything, including invoice settings and team.</summary>
    Owner = 0,

    /// <summary>Office staff: jobs, fleet, invoices; not company settings.</summary>
    Dispatcher = 1,

    /// <summary>A driver's phone: only their own jobs.</summary>
    Driver = 2,
}
