using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace OmniOps.Application;

public static class DependencyInjection
{
    /// <summary>Validators and shared application services. Handlers are registered by the Mediator source generator.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
