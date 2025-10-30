using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WAiSA.API.Security.Auditing;

/// <summary>
/// Extension methods for configuring audit logging services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds audit logging services to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAuditLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register options
        services.Configure<AuditLoggerOptions>(
            configuration.GetSection(AuditLoggerOptions.SectionName));

        // Register audit logger as singleton for performance
        services.AddSingleton<IAuditLogger, AuditLogger>();

        return services;
    }

    /// <summary>
    /// Adds audit logging services with custom configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAuditLogging(
        this IServiceCollection services,
        Action<AuditLoggerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IAuditLogger, AuditLogger>();

        return services;
    }
}
