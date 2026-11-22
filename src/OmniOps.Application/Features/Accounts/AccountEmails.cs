using Microsoft.Extensions.Logging;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Domain.Accounts;

namespace OmniOps.Application.Features.Accounts;

internal static class AccountEmails
{
    public static (string Subject, string Body) Invitation(string company, string inviter, UserRole role, string link) => (
        $"{inviter} invited you to {company} on OmniOps",
        $"""
        Hello,

        {inviter} invited you to join {company} on OmniOps as {(role == UserRole.Owner ? "an owner" : "a dispatcher")}.

        Accept the invitation and choose your password here (the link works for 7 days):
        {link}

        If you did not expect this, you can ignore this email.
        """);

    public static (string Subject, string Body) PasswordReset(string name, string link) => (
        "Choose a new OmniOps password",
        $"""
        Hello {name},

        Use this link to choose a new password. It works once, for one hour:
        {link}

        If you did not ask for this, ignore this email: your password stays the same.
        """);

    /// <summary>Sends if a mail server is configured; returns whether it was sent. Failures are logged, not thrown.</summary>
    public static async Task<bool> TrySendAsync(
        this IEmailSender email, ILogger logger, string to, (string Subject, string Body) message, CancellationToken ct)
    {
        if (!email.IsConfigured)
        {
            return false;
        }

        try
        {
            await email.SendAsync(to, message.Subject, message.Body, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not send {Subject}", message.Subject);
            return false;
        }
    }
}
