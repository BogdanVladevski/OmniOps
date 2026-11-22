using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Queries;

public enum JobScope
{
    /// <summary>Planned and on the way, plus jobs delivered in the last 24 hours.</summary>
    Active,
    Delivered,
    All
}

public record GetJobsQuery(JobScope Scope, Guid? DriverId = null) : IRequest<IReadOnlyList<JobDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetJobQuery(Guid JobId) : IRequest<JobDetailDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetJobDocumentQuery(Guid JobId, Guid DocumentId) : IRequest<JobDocumentContent>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetJobAssignablesQuery : IRequest<JobAssignablesDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

/// <summary>Anonymous: the token is the credential. Returns null for unknown, cancelled or expired links.</summary>
public record GetPublicTrackingQuery(string Token) : IRequest<PublicTrackingDto?>;
