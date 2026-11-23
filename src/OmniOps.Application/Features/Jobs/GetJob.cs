using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;

namespace OmniOps.Application.Features.Jobs;

public sealed record JobDetailDto(JobDto Job, IReadOnlyList<JobDocumentDto> Documents, IReadOnlyList<WaitDto> Waits);

[RequiresPolicy(Policies.Office)]
public sealed record GetJobQuery(Guid JobId) : IQuery<JobDetailDto>;

public sealed class GetJobHandler(IAppDbContext db, ITenantContext tenant, TimeProvider clock) : IQueryHandler<GetJobQuery, JobDetailDto>
{
    public async ValueTask<JobDetailDto> Handle(GetJobQuery query, CancellationToken cancellationToken)
    {
        var job = await db.VisibleJobAsync(tenant, query.JobId, cancellationToken);
        var terms = await WaitingTerms.ForAsync(db, tenant.OrganizationId, cancellationToken);
        return new JobDetailDto(
            JobDto.From(job),
            job.Documents.OrderBy(d => d.CapturedAtUtc).Select(JobDocumentDto.From).ToList(),
            terms.Waits(job, clock.GetUtcNow().UtcDateTime));
    }
}

[RequiresPolicy(Policies.Office)]
public sealed record GetJobDocumentQuery(Guid JobId, Guid DocumentId) : IQuery<FileContent>;

public sealed class GetJobDocumentHandler(IAppDbContext db, IFileStore files) : IQueryHandler<GetJobDocumentQuery, FileContent>
{
    public async ValueTask<FileContent> Handle(GetJobDocumentQuery query, CancellationToken cancellationToken)
    {
        var document = await db.JobDocuments.FirstOrDefaultAsync(d => d.Id == query.DocumentId && d.JobId == query.JobId, cancellationToken)
            ?? throw new KeyNotFoundException("Document not found.");
        var stream = await files.OpenReadAsync(document.StorageKey, cancellationToken)
            ?? throw new KeyNotFoundException("The file for this document is missing.");
        return new FileContent(stream, document.ContentType);
    }
}
