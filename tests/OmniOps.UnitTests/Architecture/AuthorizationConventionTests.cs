using Mediator;
using OmniOps.Application.Common.Security;

namespace OmniOps.UnitTests.Architecture;

/// <summary>
/// Authorization is declared on each message. A message that declares nothing would run for anyone, so every one must
/// say either who may send it or that it is open on purpose.
/// </summary>
public sealed class AuthorizationConventionTests
{
    private static readonly Type[] Messages = typeof(CreateJobCommand).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IMessage).IsAssignableFrom(t))
        .ToArray();

    [Fact]
    public void Every_message_declares_who_may_send_it()
    {
        var undeclared = Messages
            .Where(t => !t.IsDefined(typeof(RequiresPolicyAttribute), false) && !t.IsDefined(typeof(AnonymousAttribute), false))
            .Select(t => t.Name)
            .Order();

        Assert.Empty(undeclared);
    }

    [Fact]
    public void No_message_is_both_secured_and_anonymous()
    {
        var both = Messages.Where(t => t.IsDefined(typeof(RequiresPolicyAttribute), false) && t.IsDefined(typeof(AnonymousAttribute), false));

        Assert.Empty(both);
    }

    [Fact]
    public void Policies_named_by_messages_exist()
    {
        string[] known = [Policies.Owner, Policies.Office, Policies.JobWork];
        var unknown = Messages
            .Select(t => t.GetCustomAttributes(typeof(RequiresPolicyAttribute), false).OfType<RequiresPolicyAttribute>().FirstOrDefault())
            .Where(a => a is not null && !known.Contains(a.Policy));

        Assert.Empty(unknown);
    }
}
