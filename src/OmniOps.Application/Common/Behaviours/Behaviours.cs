using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;

namespace OmniOps.Application.Common.Behaviours;

// The pipeline every message goes through, in this order: Logging → Authorization → Validation → Transaction.

public sealed class LoggingBehaviour<TMessage, TResponse>(ILogger<LoggingBehaviour<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var started = TimeProvider.System.GetTimestamp();
        try
        {
            return await next(message, cancellationToken);
        }
        finally
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("{Message} handled in {Elapsed} ms", typeof(TMessage).Name, TimeProvider.System.GetElapsedTime(started).TotalMilliseconds);
            }
        }
    }
}

public sealed class AuthorizationBehaviour<TMessage, TResponse>(IPolicyEnforcer enforcer) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (message is IRequirePolicy secured)
        {
            await enforcer.EnforceAsync(secured.Policy, cancellationToken);
        }

        return await next(message, cancellationToken);
    }
}

public sealed class ValidationBehaviour<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(message, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(message, cancellationToken);
    }
}

public sealed class TransactionBehaviour<TMessage, TResponse>(IUnitOfWork unitOfWork) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken) =>
        message is ITransactional
            ? new ValueTask<TResponse>(unitOfWork.InTransactionAsync(ct => next(message, ct), cancellationToken))
            : next(message, cancellationToken);
}
