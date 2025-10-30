using WAiSA.API.Security.Models;

namespace WAiSA.API.Security.Filtering;

/// <summary>
/// Validates commands against agent context (role, environment, session state)
/// </summary>
public sealed class ContextValidator : IContextValidator
{
    private readonly ILogger<ContextValidator> _logger;
    private readonly Dictionary<string, SessionState> _sessionStates;

    public ContextValidator(ILogger<ContextValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionStates = new Dictionary<string, SessionState>();
    }

    /// <summary>
    /// Validate command against agent context
    /// </summary>
    public async Task<ContextValidationResult> ValidateContextAsync(
        AgentContext context,
        string command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        await Task.CompletedTask; // Placeholder for async operations

        // Validate context is valid
        if (!context.IsValid())
        {
            _logger.LogWarning("Invalid agent context: {Context}", context);
            return new ContextValidationResult(false, "Invalid agent context");
        }

        // Get or create session state
        var sessionState = GetOrCreateSessionState(context.SessionId);

        // Check for session anomalies
        var anomaly = DetectSessionAnomalies(context, sessionState, command);
        if (anomaly is not null)
        {
            _logger.LogWarning(
                "Session anomaly detected for SessionId={SessionId}: {Anomaly}",
                context.SessionId,
                anomaly);

            return new ContextValidationResult(false, $"Session anomaly: {anomaly}");
        }

        // Validate role-specific constraints
        var roleValidation = ValidateRoleConstraints(context, command);
        if (!roleValidation.IsValid)
        {
            return roleValidation;
        }

        // Validate environment-specific constraints
        var envValidation = ValidateEnvironmentConstraints(context, command);
        if (!envValidation.IsValid)
        {
            return envValidation;
        }

        // Update session state
        UpdateSessionState(sessionState, command);

        _logger.LogDebug("Context validation passed for AgentId={AgentId}, SessionId={SessionId}",
            context.AgentId, context.SessionId);

        return new ContextValidationResult(true, "Context validation passed");
    }

    /// <summary>
    /// Validate role-specific constraints
    /// </summary>
    private ContextValidationResult ValidateRoleConstraints(AgentContext context, string command)
    {
        switch (context.Role)
        {
            case AgentRole.Manual:
                // Manual mode has no automatic constraints - all commands require approval
                return new ContextValidationResult(true, null);

            case AgentRole.ReadOnly:
                // ReadOnly cannot execute write operations
                if (IsWriteOperation(command))
                {
                    return new ContextValidationResult(
                        false,
                        "ReadOnly role cannot execute write operations");
                }
                break;

            case AgentRole.LimitedWrite:
                // LimitedWrite cannot execute destructive operations
                if (IsDestructiveOperation(command))
                {
                    return new ContextValidationResult(
                        false,
                        "LimitedWrite role cannot execute destructive operations");
                }
                break;

            case AgentRole.Supervised:
                // Supervised cannot execute full autonomy operations
                if (RequiresFullAutonomy(command))
                {
                    return new ContextValidationResult(
                        false,
                        "Command requires FullAutonomy role");
                }
                break;

            case AgentRole.FullAutonomy:
                // FullAutonomy can only run in isolated/non-production environments
                if (context.Environment == AgentEnvironment.Production)
                {
                    return new ContextValidationResult(
                        false,
                        "FullAutonomy role not allowed in Production environment");
                }
                break;
        }

        return new ContextValidationResult(true, null);
    }

    /// <summary>
    /// Validate environment-specific constraints
    /// </summary>
    private ContextValidationResult ValidateEnvironmentConstraints(AgentContext context, string command)
    {
        if (context.Environment == AgentEnvironment.Production)
        {
            // Production has stricter validation
            if (IsDangerousInProduction(command))
            {
                return new ContextValidationResult(
                    false,
                    "Command is not allowed in Production environment");
            }

            // Check for development-only commands
            if (IsDevelopmentOnly(command))
            {
                return new ContextValidationResult(
                    false,
                    "Development-only command not allowed in Production");
            }
        }

        return new ContextValidationResult(true, null);
    }

    /// <summary>
    /// Detect session anomalies that might indicate compromise
    /// </summary>
    private string? DetectSessionAnomalies(AgentContext context, SessionState state, string command)
    {
        // Check for rapid command execution (potential automated attack)
        if (state.CommandCount > 100 && (DateTime.UtcNow - state.LastCommandTime).TotalSeconds < 1)
        {
            return "Unusually rapid command execution detected";
        }

        // Check for pattern changes (potential session hijacking)
        if (state.CommandCount > 10)
        {
            var currentPattern = GetCommandPattern(command);
            if (currentPattern != state.LastCommandPattern &&
                state.PatternChangeCount > 5)
            {
                return "Unusual command pattern changes detected";
            }
        }

        // Check for privilege escalation attempts
        if (state.PrivilegeEscalationAttempts > 3)
        {
            return "Multiple privilege escalation attempts detected";
        }

        return null;
    }

    private SessionState GetOrCreateSessionState(string sessionId)
    {
        if (!_sessionStates.TryGetValue(sessionId, out var state))
        {
            state = new SessionState
            {
                SessionId = sessionId,
                StartTime = DateTime.UtcNow,
                LastCommandTime = DateTime.UtcNow
            };
            _sessionStates[sessionId] = state;
        }

        return state;
    }

    private void UpdateSessionState(SessionState state, string command)
    {
        state.CommandCount++;
        state.LastCommandTime = DateTime.UtcNow;

        var currentPattern = GetCommandPattern(command);
        if (currentPattern != state.LastCommandPattern)
        {
            state.PatternChangeCount++;
            state.LastCommandPattern = currentPattern;
        }

        if (IsPrivilegeEscalationAttempt(command))
        {
            state.PrivilegeEscalationAttempts++;
        }
    }

    private static bool IsWriteOperation(string command)
    {
        var writeVerbs = new[] { "Set-", "New-", "Add-", "Update-", "Write-", "Out-", "Export-", "Remove-", "Delete-" };
        return writeVerbs.Any(verb => command.TrimStart().StartsWith(verb, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDestructiveOperation(string command)
    {
        var destructiveVerbs = new[] { "Remove-", "Delete-", "Clear-", "Stop-", "Disable-", "Format-", "Uninstall-" };
        return destructiveVerbs.Any(verb => command.TrimStart().StartsWith(verb, StringComparison.OrdinalIgnoreCase));
    }

    private static bool RequiresFullAutonomy(string command)
    {
        var fullAutonomyCommands = new[]
        {
            "Restart-Computer",
            "Stop-Computer",
            "Format-Volume",
            "Initialize-Disk",
            "Remove-Partition"
        };

        return fullAutonomyCommands.Any(cmd =>
            command.TrimStart().StartsWith(cmd, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDangerousInProduction(string command)
    {
        var dangerousPatterns = new[]
        {
            "Format-",
            "Clear-EventLog",
            "Remove-Item.*-Recurse",
            "Stop-Computer",
            "Restart-Computer"
        };

        return dangerousPatterns.Any(pattern =>
            System.Text.RegularExpressions.Regex.IsMatch(command, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    private static bool IsDevelopmentOnly(string command)
    {
        var devOnlyCommands = new[] { "Write-Debug", "Write-Verbose", "Measure-Command" };
        return devOnlyCommands.Any(cmd =>
            command.TrimStart().StartsWith(cmd, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPrivilegeEscalationAttempt(string command)
    {
        return command.Contains("sudo", StringComparison.OrdinalIgnoreCase) ||
               command.Contains("runas", StringComparison.OrdinalIgnoreCase) ||
               command.Contains("Set-ExecutionPolicy", StringComparison.OrdinalIgnoreCase) ||
               command.Contains("Add-LocalGroupMember", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCommandPattern(string command)
    {
        var parts = command.TrimStart().Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "unknown";
    }

    private sealed class SessionState
    {
        public required string SessionId { get; init; }
        public DateTime StartTime { get; init; }
        public DateTime LastCommandTime { get; set; }
        public int CommandCount { get; set; }
        public string LastCommandPattern { get; set; } = string.Empty;
        public int PatternChangeCount { get; set; }
        public int PrivilegeEscalationAttempts { get; set; }
    }
}
