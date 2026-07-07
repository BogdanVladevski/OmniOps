using MediatR;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Behaviours;

public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;
    private readonly ICorrelationContext _correlation;

    public LoggingBehaviour(
        ILogger<LoggingBehaviour<TRequest, TResponse>> logger,
        ICorrelationContext correlation)
    {
        _logger = logger;
        _correlation = correlation;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName} [CorrelationId={CorrelationId}]", requestName, _correlation.CorrelationId);

        var response = await next();

        _logger.LogInformation("Handled {RequestName} [CorrelationId={CorrelationId}]", requestName, _correlation.CorrelationId);
        return response;
    }
}
