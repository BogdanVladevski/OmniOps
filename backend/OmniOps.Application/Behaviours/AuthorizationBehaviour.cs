using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Behaviours;

public class AuthorizationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuthorizationEnforcer _enforcer;

    public AuthorizationBehaviour(IAuthorizationEnforcer enforcer)
    {
        _enforcer = enforcer;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IAuthorizedRequest authorized)
        {
            await _enforcer.EnforcePolicyAsync(authorized.RequiredPolicy, cancellationToken);
        }

        return await next();
    }
}
