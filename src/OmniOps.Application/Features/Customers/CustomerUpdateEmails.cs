using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Customers;

/// <summary>
/// System, every minute: emails customers who asked for updates when their delivery is picked up, is about half an hour
/// away, and is delivered (with the proof). Nothing happens unless email sending is set up.
/// </summary>
[Anonymous]
public sealed record ProcessCustomerUpdatesCommand : ICommand<int>;

public sealed class ProcessCustomerUpdatesHandler(
    IAppDbContext db, IEmailSender email, IPublicLinks links, TimeProvider clock, ILogger<ProcessCustomerUpdatesHandler> logger)
    : ICommandHandler<ProcessCustomerUpdatesCommand, int>
{
    public async ValueTask<int> Handle(ProcessCustomerUpdatesCommand command, CancellationToken cancellationToken)
    {
        if (!email.IsConfigured)
        {
            return 0;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var recent = now - CustomerUpdates.DeliveredStillNews;
        var candidates = await (
                from job in db.Jobs.AcrossCompanies()
                join customer in db.Customers.AcrossCompanies() on job.CustomerId equals customer.Id
                where customer.EmailUpdates && customer.Email != null && job.OrganizationId == customer.OrganizationId
                where (job.Status == JobStatus.InProgress && (job.CustomerPickedUpNoticeAtUtc == null || job.CustomerArrivingNoticeAtUtc == null))
                    || (job.Status == JobStatus.Delivered && job.CustomerDeliveredNoticeAtUtc == null && job.DeliveredAtUtc >= recent)
                select new { Job = job, customer.Email, customer.PortalToken })
            .Take(500)
            .ToListAsync(cancellationToken);

        var companies = new Dictionary<Guid, string>();
        var sent = 0;
        foreach (var item in candidates)
        {
            var job = item.Job;
            if (CustomerUpdates.Next(job, now) is not { } update)
            {
                continue;
            }

            // Saved first: an update must never go out twice, even if sending fails or a later job does.
            CustomerUpdates.MarkHandled(job, update, now);
            await db.SaveChangesAsync(cancellationToken);

            if (!companies.TryGetValue(job.OrganizationId, out var company))
            {
                companies[job.OrganizationId] = company = await db.Organizations
                    .Where(o => o.Id == job.OrganizationId).Select(o => o.Name).FirstAsync(cancellationToken);
            }

            var (subject, body) = CustomerUpdateText.For(update, job, company, links, item.PortalToken, now);
            try
            {
                await email.SendAsync(item.Email!, subject, body, cancellationToken);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Customer update {Update} for job {JobId} could not be emailed", update, job.Id);
            }
        }

        return sent;
    }
}

internal static class CustomerUpdateText
{
    public static (string Subject, string Body) For(CustomerUpdate update, Job job, string company, IPublicLinks links, string? portalToken, DateTime nowUtc)
    {
        var what = job.Reference is { } reference ? $"order {reference}" : $"your delivery to {job.Destination}";
        var track = job.TrackingToken is { } t ? links.Tracking(t) : null;
        var portal = portalToken is null ? null : $"\n\nAll your deliveries, proof and invoices: {links.Portal(portalToken)}";
        var sign = $"\n\n{company}";

        switch (update)
        {
            case CustomerUpdate.PickedUp:
                return ($"{Capitalize(what)} is on its way",
                    $"Hello,\n\n{company} has picked up {what} and it is on its way to {job.Destination}." +
                    (track is null ? string.Empty : $"\n\nFollow it live: {track}") + portal + sign);

            case CustomerUpdate.ArrivingSoon:
                var eta = Eta(job, nowUtc);
                return ($"{Capitalize(what)} arrives in about {eta} minutes",
                    $"Hello,\n\n{Capitalize(what)} is about {eta} minutes from {job.Destination}. Please have someone ready to receive it." +
                    (track is null ? string.Empty : $"\n\nFollow it live: {track}") + portal + sign);

            default:
                var proof = portalToken is null ? string.Empty : $"\n\nProof of delivery (PDF): {links.PortalProof(portalToken, job.Id)}";
                return ($"{Capitalize(what)} was delivered",
                    $"Hello,\n\n{Capitalize(what)} was delivered to {job.Destination}" +
                    (job.RecipientName is { } who ? $" and received by {who}." : ".") + proof + portal + sign);
        }
    }

    private static int Eta(Job job, DateTime nowUtc)
    {
        if (job is { LastLatitude: { } lat, LastLongitude: { } lon, LastPositionAtUtc: { } at, DestinationLatitude: { } toLat, DestinationLongitude: { } toLon })
        {
            var minutes = (DeliveryEta.Estimate(lat, lon, toLat, toLon, at).EtaUtc - nowUtc).TotalMinutes;
            return Math.Max(5, (int)Math.Round(minutes / 5) * 5);
        }

        return 30;
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
