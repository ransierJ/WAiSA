using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WAiSA.API.Security.Staging.Examples
{
    /// <summary>
    /// Demonstrates how to configure SecureStagingManager in ASP.NET Core 8.0.
    /// </summary>
    public static class DependencyInjectionSetup
    {
        /// <summary>
        /// Configures staging services in the DI container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSecureStagingServices(this IServiceCollection services)
        {
            // Register SecureStagingManager as singleton
            // Singleton is appropriate because:
            // 1. It manages a timer for periodic cleanup
            // 2. It tracks active environments across requests
            // 3. It's thread-safe with SemaphoreSlim
            services.AddSingleton<IStagingManager, SecureStagingManager>();

            return services;
        }
    }

    /// <summary>
    /// Example Program.cs configuration for ASP.NET Core 8.0.
    /// </summary>
    public class ProgramExample
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            // Register SecureStagingManager
            builder.Services.AddSecureStagingServices();

            // Alternative: Register with custom configuration
            // builder.Services.AddSingleton<IStagingManager>(serviceProvider =>
            // {
            //     var logger = serviceProvider.GetRequiredService<ILogger<SecureStagingManager>>();
            //     return new SecureStagingManager(logger);
            // });

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            // Optional: Ensure base staging directory exists on startup
            var stagingManager = app.Services.GetRequiredService<IStagingManager>();
            var basePath = stagingManager.GetBaseStagingPath();

            if (!System.IO.Directory.Exists(basePath))
            {
                System.IO.Directory.CreateDirectory(basePath);
                app.Logger.LogInformation("Created base staging directory: {BasePath}", basePath);
            }

            app.Run();
        }
    }
}
