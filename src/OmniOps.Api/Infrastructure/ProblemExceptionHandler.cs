using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OmniOps.Domain.Common;

namespace OmniOps.Api.Infrastructure;

/// <summary>
/// Turns exceptions into RFC 9457 problem details with a message a person can act on:
/// validation 400, sign-in 401, access 403, not found 404, business rules 409, anything else 500 (details logged, not shown).
/// </summary>
internal sealed class ProblemExceptionHandler(IProblemDetailsService problems, ILogger<ProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Some details need fixing."),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "The request could not be read."),
            AuthenticationFailedException e => (StatusCodes.Status401Unauthorized, e.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "You do not have access to this."),
            KeyNotFoundException e => (StatusCodes.Status404NotFound, e.Message),
            BusinessRuleException e => (StatusCodes.Status409Conflict, e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Something went wrong on our side. Please try again."),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        var problem = new ProblemDetails { Status = status, Title = title };
        if (exception is ValidationException validation)
        {
            // The first message is the one to show; all of them, per field, for forms.
            problem.Title = validation.Errors.FirstOrDefault()?.ErrorMessage ?? title;
            problem.Extensions["errors"] = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem, Exception = exception });
    }
}
