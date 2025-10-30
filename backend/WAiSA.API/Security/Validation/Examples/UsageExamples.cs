using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WAiSA.API.Security.Validation;
using WAiSA.API.Security.Validation.Models;

namespace WAiSA.API.Security.Validation.Examples;

/// <summary>
/// Example usage patterns for InputValidator
/// </summary>
public class UsageExamples
{
    private readonly IInputValidator _validator;
    private readonly ILogger<UsageExamples> _logger;

    public UsageExamples(IInputValidator validator, ILogger<UsageExamples> logger)
    {
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Example 1: Basic command validation in API controller
    /// </summary>
    public IActionResult ValidateApiCommand(string command, Dictionary<string, string>? parameters)
    {
        var result = _validator.ValidateCommand(command, parameters);

        if (!result.IsValid)
        {
            // Log the security violation
            _logger.LogWarning(
                "Command validation failed. Severity: {Severity}, Failures: {FailureCount}",
                result.Severity,
                result.Failures.Count);

            // Return detailed error for debugging (sanitize in production)
            return new BadRequestObjectResult(new
            {
                Error = "Invalid command detected",
                Severity = result.Severity.ToString(),
                Violations = result.Failures.Select(f => new
                {
                    Type = f.Type.ToString(),
                    f.Message,
                    f.Pattern
                }).ToList()
            });
        }

        return new OkObjectResult("Command is valid");
    }

    /// <summary>
    /// Example 2: Multi-stage validation with sanitization
    /// </summary>
    public async Task<bool> ProcessAgentCommandAsync(
        string command,
        Dictionary<string, string> parameters)
    {
        // Stage 1: Validate command structure
        var validationResult = _validator.ValidateCommand(command, parameters);
        if (!validationResult.IsValid)
        {
            if (validationResult.Severity >= ValidationSeverity.High)
            {
                _logger.LogError(
                    "Critical security violation detected. Command rejected. Failures: {Failures}",
                    string.Join(", ", validationResult.Failures.Select(f => f.Message)));
                return false;
            }
        }

        // Stage 2: Check for specific injection patterns
        var injectionCheck = _validator.CheckForInjectionPatterns(command);
        if (!injectionCheck.IsValid)
        {
            _logger.LogWarning("Injection patterns detected: {Patterns}",
                string.Join(", ", injectionCheck.Failures.Select(f => f.Pattern)));
            return false;
        }

        // Stage 3: Check for path traversal
        var pathViolations = _validator.CheckForPathTraversal(parameters);
        if (pathViolations.Any())
        {
            _logger.LogWarning("Path traversal attempts: {Count}", pathViolations.Count);
            return false;
        }

        // Stage 4: Sanitize parameters before use
        var sanitizedParams = _validator.SanitizeParameters(parameters);

        // Stage 5: Execute with sanitized inputs
        await ExecuteSafeCommandAsync(command, sanitizedParams);

        return true;
    }

    /// <summary>
    /// Example 3: Custom validation with specific severity handling
    /// </summary>
    public ValidationResponse ValidateWithSeverityHandling(string command)
    {
        var result = _validator.ValidateCommand(command);

        return result.Severity switch
        {
            ValidationSeverity.None => new ValidationResponse
            {
                IsAllowed = true,
                Message = "Command validated successfully"
            },
            ValidationSeverity.Low => new ValidationResponse
            {
                IsAllowed = true,
                Message = "Command has minor issues but is allowed",
                Warnings = result.Failures.Select(f => f.Message).ToList()
            },
            ValidationSeverity.Medium => new ValidationResponse
            {
                IsAllowed = false,
                Message = "Command contains medium severity violations",
                Errors = result.Failures.Select(f => f.Message).ToList()
            },
            ValidationSeverity.High or ValidationSeverity.Critical => new ValidationResponse
            {
                IsAllowed = false,
                Message = "Command rejected due to critical security violations",
                Errors = result.Failures.Select(f => f.Message).ToList(),
                RequiresIncidentReport = true
            },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// Example 4: Validation with rate limiting for repeated violations
    /// </summary>
    public class ValidatorWithRateLimiting
    {
        private readonly IInputValidator _validator;
        private readonly Dictionary<string, ViolationTracker> _violationTrackers = new();

        public ValidatorWithRateLimiting(IInputValidator validator)
        {
            _validator = validator;
        }

        public (bool IsValid, bool IsBlocked) ValidateWithTracking(string userId, string command)
        {
            if (!_violationTrackers.TryGetValue(userId, out var tracker))
            {
                tracker = new ViolationTracker();
                _violationTrackers[userId] = tracker;
            }

            // Check if user is blocked due to repeated violations
            if (tracker.IsBlocked)
            {
                return (false, true);
            }

            var result = _validator.ValidateCommand(command);

            if (!result.IsValid && result.Severity >= ValidationSeverity.High)
            {
                tracker.RecordViolation();

                // Block user after 3 critical violations within time window
                if (tracker.ViolationCount >= 3)
                {
                    tracker.Block();
                    return (false, true);
                }
            }

            return (result.IsValid, false);
        }

        private class ViolationTracker
        {
            private readonly Queue<DateTime> _violations = new();
            private readonly TimeSpan _window = TimeSpan.FromMinutes(10);
            public bool IsBlocked { get; private set; }
            public int ViolationCount => _violations.Count;

            public void RecordViolation()
            {
                var now = DateTime.UtcNow;
                _violations.Enqueue(now);

                // Remove old violations outside the time window
                while (_violations.Count > 0 && now - _violations.Peek() > _window)
                {
                    _violations.Dequeue();
                }
            }

            public void Block()
            {
                IsBlocked = true;
            }
        }
    }

    /// <summary>
    /// Example 5: Batch validation for multiple commands
    /// </summary>
    public async Task<BatchValidationResult> ValidateBatchAsync(
        IEnumerable<CommandRequest> commands)
    {
        var results = new List<CommandValidationResult>();

        foreach (var cmd in commands)
        {
            var result = _validator.ValidateCommand(cmd.Command, cmd.Parameters);
            results.Add(new CommandValidationResult
            {
                CommandId = cmd.Id,
                Command = cmd.Command,
                IsValid = result.IsValid,
                Severity = result.Severity,
                Failures = result.Failures.ToList()
            });
        }

        var hasBlockingFailures = results.Any(r => r.Severity >= ValidationSeverity.High);

        return new BatchValidationResult
        {
            TotalCommands = results.Count,
            ValidCommands = results.Count(r => r.IsValid),
            InvalidCommands = results.Count(r => !r.IsValid),
            HasBlockingFailures = hasBlockingFailures,
            Results = results
        };
    }

    /// <summary>
    /// Example 6: Integration with middleware for automatic validation
    /// </summary>
    public class ValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IInputValidator _validator;
        private readonly ILogger<ValidationMiddleware> _logger;

        public ValidationMiddleware(
            RequestDelegate next,
            IInputValidator validator,
            ILogger<ValidationMiddleware> logger)
        {
            _next = next;
            _validator = validator;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only validate specific endpoints
            if (context.Request.Path.StartsWithSegments("/api/agent/execute"))
            {
                // Read and parse request body
                context.Request.EnableBuffering();
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                context.Request.Body.Position = 0;

                // Extract command from request (simplified)
                if (!string.IsNullOrEmpty(body))
                {
                    var command = ExtractCommand(body);
                    var result = _validator.ValidateCommand(command);

                    if (!result.IsValid && result.Severity >= ValidationSeverity.High)
                    {
                        _logger.LogWarning(
                            "Blocked malicious command attempt from {IP}",
                            context.Connection.RemoteIpAddress);

                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            Error = "Invalid command detected",
                            Severity = result.Severity.ToString()
                        });
                        return;
                    }
                }
            }

            await _next(context);
        }

        private string ExtractCommand(string body)
        {
            // Simplified - implement actual JSON parsing
            return body;
        }
    }

    #region Helper Classes

    public class ValidationResponse
    {
        public bool IsAllowed { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public bool RequiresIncidentReport { get; set; }
    }

    public class CommandRequest
    {
        public string Id { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new();
    }

    public class CommandValidationResult
    {
        public string CommandId { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public ValidationSeverity Severity { get; set; }
        public List<ValidationFailure> Failures { get; set; } = new();
    }

    public class BatchValidationResult
    {
        public int TotalCommands { get; set; }
        public int ValidCommands { get; set; }
        public int InvalidCommands { get; set; }
        public bool HasBlockingFailures { get; set; }
        public List<CommandValidationResult> Results { get; set; } = new();
    }

    #endregion

    private Task ExecuteSafeCommandAsync(string command, Dictionary<string, string> parameters)
    {
        // Implement safe command execution
        return Task.CompletedTask;
    }
}
