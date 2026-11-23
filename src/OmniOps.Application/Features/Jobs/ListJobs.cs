using System.Text.Json.Serialization;
using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Fleet;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Jobs;

[JsonConverter(typeof(JsonStringEnumConverter<JobScope>))]
public enum JobScope
{
    /// <summary>Planned and on the way, plus jobs delivered in the last 24 hours.</summary>
    Active,
    Delivered,
    All,
}

/// <summary>Office: the company's jobs. Driver: only their own, whatever else is asked for.</summary>
[RequiresPolicy(Policies.JobWork)]
public sealed record ListJobsQuery(JobScope Scope) : IQuery<IReadOnlyList<JobDto>>;

public sealed class ListJobsHandler(IAppDbContext db, ITenantContext tenant, TimeProvider clock) : IQueryHandler<ListJobsQuery, IReadOnlyList<JobDto>>
{
    private const int MaxJobs = 200;

    public async ValueTask<IReadOnlyList<JobDto>> Handle(ListJobsQuery query, CancellationToken cancellationToken)
    {
        var since = clock.GetUtcNow().UtcDateTime.AddHours(-24);
        var jobs = db.Visible(tenant).Where(j => j.Status != JobStatus.Cancelled);
        jobs = query.Scope switch
        {
            JobScope.Active => jobs.Where(j => j.Status != JobStatus.Delivered || j.DeliveredAtUtc >= since),
            JobScope.Delivered => jobs.Where(j => j.Status == JobStatus.Delivered),
            _ => jobs,
        };

        var list = await jobs.OrderByDescending(j => j.CreatedAtUtc).Take(MaxJobs).ToListAsync(cancellationToken);
        return list.Select(JobDto.From).ToList();
    }
}

/// <summary>Trucks and drivers to choose from when creating a job.</summary>
public sealed record JobOptionsDto(IReadOnlyList<VehicleDto> Vehicles, IReadOnlyList<DriverDto> Drivers);

[RequiresPolicy(Policies.Office)]
public sealed record GetJobOptionsQuery : IQuery<JobOptionsDto>;

public sealed class GetJobOptionsHandler(IMediator mediator) : IQueryHandler<GetJobOptionsQuery, JobOptionsDto>
{
    public async ValueTask<JobOptionsDto> Handle(GetJobOptionsQuery query, CancellationToken cancellationToken) =>
        new(await mediator.Send(new ListVehiclesQuery(), cancellationToken), await mediator.Send(new ListDriversQuery(), cancellationToken));
}
