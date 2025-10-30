using Microsoft.AspNetCore.Mvc;
using WAiSA.API.Security.Guards;

namespace WAiSA.API.Security.Guards.Examples;

/// <summary>
/// Example integration of LateralMovementGuard in a controller.
/// This demonstrates best practices for command validation in ASP.NET Core 8.0.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SecureCommandController : ControllerBase
{
    private readonly ILateralMovementGuard _lateralMovementGuard;
    private readonly ILogger<SecureCommandController> _logger;

    public SecureCommandController(
        ILateralMovementGuard lateralMovementGuard,
        ILogger<SecureCommandController> logger)
    {
        _lateralMovementGuard = lateralMovementGuard;
        _logger = logger;
    }

    /// <summary>
    /// Executes a PowerShell command with lateral movement protection.
    /// </summary>
    /// <param name="request">The command execution request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result or validation error.</returns>
    [HttpPost("execute")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExecuteCommand(
        [FromBody] CommandExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return BadRequest(new { error = "Command is required" });
        }

        try
        {
            // Validate against lateral movement
            var validation = await _lateralMovementGuard.ValidateCommandAsync(
                request.Command,
                cancellationToken);

            if (!validation.IsAllowed)
            {
                _logger.LogWarning(
                    "Command blocked due to lateral movement violation. " +
                    "User: {User}, Reason: {Reason}, Violation: {Type}",
                    User.Identity?.Name ?? "Anonymous",
                    validation.BlockedReason,
                    validation.ViolationType);

                if (validation.ShouldQuarantine)
                {
                    // Trigger agent quarantine
                    await QuarantineAgentAsync(User.Identity?.Name);
                }

                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new CommandValidationResponse
                    {
                        Success = false,
                        Error = validation.BlockedReason,
                        ViolationType = validation.ViolationType?.ToString(),
                        Quarantined = validation.ShouldQuarantine,
                        Context = validation.Context
                    });
            }

            // Command passed validation - execute
            _logger.LogInformation(
                "Executing validated command for user: {User}",
                User.Identity?.Name ?? "Anonymous");

            var result = await ExecuteCommandInternalAsync(request.Command, cancellationToken);

            return Ok(new CommandExecutionResponse
            {
                Success = true,
                Output = result.Output,
                ExitCode = result.ExitCode,
                ExecutionTime = result.ExecutionTime
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Command execution cancelled by user");
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "Internal server error during command execution" });
        }
    }

    /// <summary>
    /// Reloads the lateral movement guard configuration.
    /// Requires admin role.
    /// </summary>
    [HttpPost("reload-config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReloadConfiguration()
    {
        // Note: Add [Authorize(Roles = "Admin")] attribute in production
        try
        {
            await _lateralMovementGuard.ReloadConfigurationAsync();

            _logger.LogInformation(
                "Configuration reloaded by user: {User}",
                User.Identity?.Name ?? "Anonymous");

            return Ok(new { message = "Configuration reloaded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload configuration");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "Failed to reload configuration" });
        }
    }

    /// <summary>
    /// Validates a command without executing it.
    /// Useful for pre-flight validation in UI.
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateCommand(
        [FromBody] CommandValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return BadRequest(new { error = "Command is required" });
        }

        var validation = await _lateralMovementGuard.ValidateCommandAsync(
            request.Command,
            cancellationToken);

        return Ok(new CommandValidationResponse
        {
            Success = validation.IsAllowed,
            Error = validation.BlockedReason,
            ViolationType = validation.ViolationType?.ToString(),
            Quarantined = validation.ShouldQuarantine,
            Context = validation.Context
        });
    }

    // Private helper methods

    private async Task<CommandResult> ExecuteCommandInternalAsync(
        string command,
        CancellationToken cancellationToken)
    {
        // Placeholder for actual command execution logic
        // In production, this would interface with PowerShell runtime
        await Task.Delay(100, cancellationToken);

        return new CommandResult
        {
            Output = "Command executed successfully",
            ExitCode = 0,
            ExecutionTime = TimeSpan.FromMilliseconds(100)
        };
    }

    private async Task QuarantineAgentAsync(string? username)
    {
        // Placeholder for agent quarantine logic
        // In production, this would:
        // 1. Mark agent as quarantined in database
        // 2. Send notification to security team
        // 3. Log security event
        // 4. Trigger incident response workflow

        _logger.LogCritical(
            "Agent quarantined due to lateral movement attempt. User: {User}",
            username ?? "Unknown");

        await Task.CompletedTask;
    }

    // Request/Response models

    public sealed class CommandExecutionRequest
    {
        public string Command { get; set; } = string.Empty;
        public Dictionary<string, string>? Parameters { get; set; }
    }

    public sealed class CommandValidationRequest
    {
        public string Command { get; set; } = string.Empty;
    }

    public sealed class CommandExecutionResponse
    {
        public bool Success { get; set; }
        public string? Output { get; set; }
        public int ExitCode { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }

    public sealed class CommandValidationResponse
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? ViolationType { get; set; }
        public bool Quarantined { get; set; }
        public Dictionary<string, string>? Context { get; set; }
    }

    private sealed class CommandResult
    {
        public string Output { get; set; } = string.Empty;
        public int ExitCode { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }
}

/// <summary>
/// Example middleware for applying lateral movement guard to all command executions.
/// </summary>
public sealed class LateralMovementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LateralMovementMiddleware> _logger;

    public LateralMovementMiddleware(
        RequestDelegate next,
        ILogger<LateralMovementMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ILateralMovementGuard guard)
    {
        // Only process command execution endpoints
        if (!context.Request.Path.StartsWithSegments("/api/command"))
        {
            await _next(context);
            return;
        }

        // Enable buffering to read request body multiple times
        context.Request.EnableBuffering();

        // Extract command from request body
        var command = await ExtractCommandFromRequestAsync(context.Request);

        if (!string.IsNullOrWhiteSpace(command))
        {
            var validation = await guard.ValidateCommandAsync(command);

            if (!validation.IsAllowed)
            {
                _logger.LogWarning(
                    "Request blocked by lateral movement middleware. " +
                    "Path: {Path}, Reason: {Reason}",
                    context.Request.Path,
                    validation.BlockedReason);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Command blocked by security policy",
                    reason = validation.BlockedReason,
                    violationType = validation.ViolationType?.ToString()
                });

                return;
            }
        }

        // Reset stream position for next middleware
        context.Request.Body.Position = 0;

        await _next(context);
    }

    private static async Task<string?> ExtractCommandFromRequestAsync(HttpRequest request)
    {
        try
        {
            if (request.ContentType?.Contains("application/json") != true)
            {
                return null;
            }

            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();

            // Reset position for next read
            request.Body.Position = 0;

            // Simple extraction - in production use proper JSON parsing
            var commandMatch = System.Text.RegularExpressions.Regex.Match(
                body,
                @"""command""\s*:\s*""([^""]+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return commandMatch.Success ? commandMatch.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Extension methods for registering LateralMovementGuard services.
/// </summary>
public static class LateralMovementGuardServiceExtensions
{
    /// <summary>
    /// Adds lateral movement guard services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddLateralMovementGuard(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Register the guard as singleton for performance
        services.AddSingleton<ILateralMovementGuard>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<LateralMovementGuard>>();

            // Get config path from configuration or use default
            var configPath = configuration["Security:LateralMovement:ConfigPath"]
                ?? Path.Combine(environment.ContentRootPath, "..", "..", "ai-agent-guardrails-enhanced.yml");

            return new LateralMovementGuard(logger, configPath);
        });

        return services;
    }

    /// <summary>
    /// Adds the lateral movement middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseLateralMovementGuard(
        this IApplicationBuilder app)
    {
        app.UseMiddleware<LateralMovementMiddleware>();
        return app;
    }
}

/// <summary>
/// Example usage in Program.cs for ASP.NET Core 8.0 minimal APIs.
/// </summary>
public static class ProgramConfigurationExample
{
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        // Add lateral movement guard
        builder.Services.AddLateralMovementGuard(
            builder.Configuration,
            builder.Environment);

        // Add controllers
        builder.Services.AddControllers();
    }

    public static void ConfigureMiddleware(WebApplication app)
    {
        // Add lateral movement middleware early in pipeline
        app.UseLateralMovementGuard();

        // Other middleware
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
    }
}
