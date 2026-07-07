using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Behaviours;

public class TransactionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ITransactionManager _transactionManager;

    public TransactionBehaviour(ITransactionManager transactionManager)
    {
        _transactionManager = transactionManager;
    }

    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ITransactionalRequest)
        {
            return next();
        }

        return _transactionManager.ExecuteInTransactionAsync(_ => next(), cancellationToken);
    }
}
