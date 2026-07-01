using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OmniOps.Api.Middleware;

public static class GlobalExceptionMiddleware
{
    public static void UseGlobalExceptionHandler(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                var exception = exceptionFeature?.Error;
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

                logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                    context.Request.Method, context.Request.Path);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = exception switch
                {
                    ValidationException => (int)HttpStatusCode.BadRequest,
                    ArgumentException => (int)HttpStatusCode.BadRequest,
                    KeyNotFoundException => (int)HttpStatusCode.NotFound,
                    _ => (int)HttpStatusCode.InternalServerError
                };

                var problem = new ProblemDetails
                {
                    Status = context.Response.StatusCode,
                    Title = exception switch
                    {
                        ValidationException => "Validation failed.",
                        _ => "An error occurred processing your request."
                    },
                    Detail = app.Environment.IsDevelopment() ? exception?.Message : null
                };

                if (exception is ValidationException validationException)
                {
                    problem.Extensions["errors"] = validationException.Errors
                        .GroupBy(error => error.PropertyName)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Select(error => error.ErrorMessage).ToArray());
                }

                await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
            });
        });
    }
}
