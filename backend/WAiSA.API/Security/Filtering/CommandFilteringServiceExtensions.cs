using Microsoft.Extensions.DependencyInjection;
using WAiSA.API.Security.Configuration;

namespace WAiSA.API.Security.Filtering;

/// <summary>
/// Service registration extensions for command filtering
/// </summary>
public static class CommandFilteringServiceExtensions
{
    /// <summary>
    /// Add command filtering services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddCommandFiltering(
        this IServiceCollection services,
        IConfiguration configuration,
        string? configPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Register configuration
        services.AddGuardrailsConfiguration(configuration, configPath);

        // Register core filtering components
        services.AddSingleton<ICommandFilteringEngine, CommandFilteringEngine>();
        services.AddSingleton<ISemanticAnalyzer, SemanticAnalyzer>();
        services.AddSingleton<IContextValidator, ContextValidator>();
        services.AddSingleton<IRateLimiter, RateLimiter>();

        return services;
    }

    /// <summary>
    /// Add command filtering services with custom configuration
    /// </summary>
    public static IServiceCollection AddCommandFiltering(
        this IServiceCollection services,
        Action<CommandFilteringConfig> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // Register with custom configuration
        services.Configure(configureOptions);

        // Register core filtering components
        services.AddSingleton<ICommandFilteringEngine, CommandFilteringEngine>();
        services.AddSingleton<ISemanticAnalyzer, SemanticAnalyzer>();
        services.AddSingleton<IContextValidator, ContextValidator>();
        services.AddSingleton<IRateLimiter, RateLimiter>();

        return services;
    }
}
