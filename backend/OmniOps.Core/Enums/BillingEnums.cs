namespace OmniOps.Core.Enums;

/// <summary>Stored as integers: never renumber. "Overdue" is not a status — it is derived from the due date.</summary>
public enum InvoiceStatus
{
    Issued = 0,
    Sent = 1,
    Paid = 2,
}

/// <summary>Stored as integers: never renumber.</summary>
public enum ReminderKind
{
    DueSoon = 0,
    DueToday = 1,
    Overdue7 = 2,
    Overdue14 = 3,
}
