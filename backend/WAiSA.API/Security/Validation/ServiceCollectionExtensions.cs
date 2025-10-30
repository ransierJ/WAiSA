using Microsoft.Extensions.DependencyInjection;

namespace WAiSA.API.Security.Validation;

/// <summary>
/// Extension methods for registering validation services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds input validation services to the service collection
    /// </summary>
    public static IServiceCollection AddInputValidation(this IServiceCollection services)
    {
        services.AddScoped<IInputValidator, InputValidator>();
        return services;
    }
}
