using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Queries;

public record GetBillingProfileQuery : IRequest<BillingProfileDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetInvoiceOverviewQuery : IRequest<InvoiceOverviewDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetInvoiceQuery(Guid InvoiceId) : IRequest<InvoiceDetailDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

/// <summary>Anonymous: the token is the credential. Null for unknown tokens.</summary>
public record GetPublicInvoiceQuery(string Token) : IRequest<PublicInvoiceDto?>;

/// <summary>Anonymous: a photo from the invoiced delivery, reachable only through that invoice's token.</summary>
public record GetPublicInvoiceDocumentQuery(string Token, Guid DocumentId) : IRequest<JobDocumentContent?>;
