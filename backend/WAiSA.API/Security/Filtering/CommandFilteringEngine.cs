using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WAiSA.API.Security.Models;
using WAiSA.API.Security.Configuration;

namespace WAiSA.API.Security.Filtering;

/// <summary>
/// Interface for command filtering engine with multi-layer validation
/// </summary>
public interface ICommandFilteringEngine
{
    /// <summary>
    /// Filters a command through multiple validation layers
    /// </summary>
    Task<CommandFilterResult> FilterCommandAsync(
        AgentContext context,
        string command,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a command is whitelisted for a specific role and environment
    /// </summary>
    bool IsCommandWhitelisted(string command, AgentRole role, AgentEnvironment environment);

    /// <summary>
    /// Checks if a command matches any blacklist patterns
    /// </summary>
    (bool IsBlacklisted, string? MatchedPattern) IsCommandBlacklisted(string command);

    /// <summary>
    /// Validates command parameters against allowed configurations
    /// </summary>
    Task<ParameterValidationResult> ValidateParametersAsync(
        string command,
        Dictionary<string, string> parameters,
        AllowedParameters allowedConfig,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Multi-layer command filtering engine for AI agent security
/// </summary>
public sealed class CommandFilteringEngine : ICommandFilteringEngine
{
    private readonly ILogger<CommandFilteringEngine> _logger;
    private readonly CommandFilteringConfig _config;
    private readonly ISemanticAnalyzer _semanticAnalyzer;
    private readonly IContextValidator _contextValidator;
    private readonly IRateLimiter _rateLimiter;

    // Compiled regex patterns for performance
    private readonly Dictionary<string, Regex> _blacklistPatterns;
    private readonly Dictionary<string, Regex> _whitelistPatterns;
    private readonly Dictionary<string, Regex> _parameterPatterns;

    public CommandFilteringEngine(
        ILogger<CommandFilteringEngine> logger,
        IOptions<CommandFilteringConfig> config,
        ISemanticAnalyzer semanticAnalyzer,
        IContextValidator contextValidator,
        IRateLimiter rateLimiter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _semanticAnalyzer = semanticAnalyzer ?? throw new ArgumentNullException(nameof(semanticAnalyzer));
        _contextValidator = contextValidator ?? throw new ArgumentNullException(nameof(contextValidator));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));

        // Initialize compiled patterns for performance
        _blacklistPatterns = CompilePatterns(_config.Blacklist?.Patterns ?? new List<string>());
        _whitelistPatterns = new Dictionary<string, Regex>();
        _parameterPatterns = new Dictionary<string, Regex>();

        _logger.LogInformation(
            "CommandFilteringEngine initialized with {BlacklistCount} blacklist patterns and {LayerCount} validation layers",
            _blacklistPatterns.Count,
            _config.ValidationLayers?.Count ?? 0);
    }

    /// <summary>
    /// Filters command through multi-layer validation pipeline
    /// </summary>
    public async Task<CommandFilterResult> FilterCommandAsync(
        AgentContext context,
        string command,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        var startTime = DateTime.UtcNow;
        var validationLayers = _config.ValidationLayers ?? new List<string> { "syntax", "blacklist", "whitelist", "semantic", "context", "rate-limit" };

        _logger.LogDebug(
            "Starting command filtering for AgentId={AgentId}, Role={Role}, Environment={Environment}, Command={Command}",
            context.AgentId,
            context.Role,
            context.Environment,
            command);

        try
        {
            // Layer 1: Syntax Validation
            if (validationLayers.Contains("syntax"))
            {
                var syntaxResult = ValidateSyntax(command, parameters);
                if (!syntaxResult.IsValid)
                {
                    return CreateFilterResult(false, FilterReason.InvalidSyntax, syntaxResult.Reason, context, command);
                }
            }

            // Layer 2: Blacklist Check
            if (validationLayers.Contains("blacklist"))
            {
                var (isBlacklisted, pattern) = IsCommandBlacklisted(command);
                if (isBlacklisted)
                {
                    _logger.LogWarning(
                        "Command blocked by blacklist pattern. AgentId={AgentId}, Pattern={Pattern}, Command={Command}",
                        context.AgentId,
                        pattern,
                        command);

                    return CreateFilterResult(false, FilterReason.Blacklisted, $"Matched blacklist pattern: {pattern}", context, command);
                }
            }

            // Layer 3: Whitelist Check
            if (validationLayers.Contains("whitelist"))
            {
                if (!IsCommandWhitelisted(command, context.Role, context.Environment))
                {
                    _logger.LogWarning(
                        "Command not in whitelist. AgentId={AgentId}, Role={Role}, Environment={Environment}, Command={Command}",
                        context.AgentId,
                        context.Role,
                        context.Environment,
                        command);

                    return CreateFilterResult(false, FilterReason.NotWhitelisted, "Command not in role-based whitelist", context, command);
                }
            }

            // Layer 4: Parameter Validation
            if (parameters is not null && parameters.Count > 0)
            {
                var allowedParams = GetAllowedParameters(command, context.Role);
                var paramResult = await ValidateParametersAsync(command, parameters, allowedParams, cancellationToken);

                if (!paramResult.IsValid)
                {
                    return CreateFilterResult(false, FilterReason.InvalidParameters, paramResult.Reason, context, command);
                }
            }

            // Layer 5: Semantic Analysis
            if (validationLayers.Contains("semantic"))
            {
                var semanticResult = await _semanticAnalyzer.AnalyzeCommandAsync(command, parameters, cancellationToken);
                if (!semanticResult.IsAllowed)
                {
                    return CreateFilterResult(false, FilterReason.SemanticViolation, semanticResult.Reason, context, command);
                }
            }

            // Layer 6: Context Validation
            if (validationLayers.Contains("context"))
            {
                var contextResult = await _contextValidator.ValidateContextAsync(context, command, cancellationToken);
                if (!contextResult.IsValid)
                {
                    return CreateFilterResult(false, FilterReason.ContextViolation, contextResult.Reason, context, command);
                }
            }

            // Layer 7: Rate Limiting
            if (validationLayers.Contains("rate-limit"))
            {
                var rateLimitResult = await _rateLimiter.CheckRateLimitAsync(context, cancellationToken);
                if (!rateLimitResult.IsAllowed)
                {
                    return CreateFilterResult(false, FilterReason.RateLimitExceeded, "Rate limit exceeded", context, command);
                }
            }

            // Determine if approval is required
            var requiresApproval = DetermineApprovalRequirement(context, command, parameters);

            var result = CreateFilterResult(true, FilterReason.Allowed, "All validation layers passed", context, command, requiresApproval);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "Command filtering completed. AgentId={AgentId}, IsAllowed={IsAllowed}, RequiresApproval={RequiresApproval}, Duration={Duration}ms",
                context.AgentId,
                result.IsAllowed,
                result.RequiresApproval,
                duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering command for AgentId={AgentId}, Command={Command}", context.AgentId, command);
            return CreateFilterResult(false, FilterReason.InternalError, $"Internal error: {ex.Message}", context, command);
        }
    }

    /// <summary>
    /// Checks if command is whitelisted for specific role and environment
    /// </summary>
    public bool IsCommandWhitelisted(string command, AgentRole role, AgentEnvironment environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        var whitelist = GetWhitelistForRole(role, environment);
        if (whitelist is null || whitelist.Count == 0)
        {
            _logger.LogDebug("No whitelist configured for Role={Role}, Environment={Environment}", role, environment);
            return false;
        }

        // Extract command name (handles both "Get-Process" and "Get-Process -Name foo")
        var commandName = ExtractCommandName(command);

        // Check exact match
        if (whitelist.Contains(commandName, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check wildcard patterns (e.g., "Get-*", "Test-*")
        foreach (var pattern in whitelist)
        {
            if (pattern.Contains('*'))
            {
                var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                if (Regex.IsMatch(commandName, regexPattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if command matches blacklist patterns
    /// </summary>
    public (bool IsBlacklisted, string? MatchedPattern) IsCommandBlacklisted(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        foreach (var kvp in _blacklistPatterns)
        {
            if (kvp.Value.IsMatch(command))
            {
                return (true, kvp.Key);
            }
        }

        return (false, null);
    }

    /// <summary>
    /// Validates command parameters against allowed configurations
    /// </summary>
    public async Task<ParameterValidationResult> ValidateParametersAsync(
        string command,
        Dictionary<string, string> parameters,
        AllowedParameters allowedConfig,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(allowedConfig);

        // Check parameter count
        if (_config.InputConstraints is not null)
        {
            if (parameters.Count > _config.InputConstraints.MaxParameters)
            {
                return new ParameterValidationResult(
                    false,
                    $"Too many parameters: {parameters.Count} exceeds maximum {_config.InputConstraints.MaxParameters}");
            }
        }

        // Validate each parameter
        foreach (var param in parameters)
        {
            // Check parameter name length
            if (_config.InputConstraints is not null && param.Key.Length > _config.InputConstraints.MaxParameterLength)
            {
                return new ParameterValidationResult(
                    false,
                    $"Parameter name too long: {param.Key.Length} exceeds maximum {_config.InputConstraints.MaxParameterLength}");
            }

            // Check parameter value length
            if (_config.InputConstraints is not null && param.Value.Length > _config.InputConstraints.MaxParameterLength)
            {
                return new ParameterValidationResult(
                    false,
                    $"Parameter value too long for '{param.Key}': {param.Value.Length} exceeds maximum {_config.InputConstraints.MaxParameterLength}");
            }

            // Check if parameter is allowed
            if (allowedConfig.ParameterNames is not null && !allowedConfig.ParameterNames.Contains(param.Key, StringComparer.OrdinalIgnoreCase))
            {
                return new ParameterValidationResult(
                    false,
                    $"Parameter '{param.Key}' is not allowed for command '{command}'");
            }

            // Check parameter value patterns
            if (allowedConfig.ValuePatterns is not null && allowedConfig.ValuePatterns.TryGetValue(param.Key, out var pattern))
            {
                if (!Regex.IsMatch(param.Value, pattern, RegexOptions.IgnoreCase))
                {
                    return new ParameterValidationResult(
                        false,
                        $"Parameter '{param.Key}' value does not match allowed pattern");
                }
            }

            // Detect injection attempts in parameters
            var injectionCheck = await DetectParameterInjectionAsync(param.Key, param.Value, cancellationToken);
            if (!injectionCheck.IsValid)
            {
                return injectionCheck;
            }
        }

        return new ParameterValidationResult(true, "All parameters validated successfully");
    }

    #region Private Helper Methods

    private SyntaxValidationResult ValidateSyntax(string command, Dictionary<string, string>? parameters)
    {
        // Check command length
        if (_config.InputConstraints is not null && command.Length > _config.InputConstraints.MaxCommandLength)
        {
            return new SyntaxValidationResult(
                false,
                $"Command too long: {command.Length} exceeds maximum {_config.InputConstraints.MaxCommandLength}");
        }

        // Check for balanced quotes
        var quoteCount = command.Count(c => c == '"' || c == '\'');
        if (quoteCount % 2 != 0)
        {
            return new SyntaxValidationResult(false, "Unbalanced quotes in command");
        }

        // Check for balanced brackets
        var openBrackets = command.Count(c => c == '{' || c == '(' || c == '[');
        var closeBrackets = command.Count(c => c == '}' || c == ')' || c == ']');
        if (openBrackets != closeBrackets)
        {
            return new SyntaxValidationResult(false, "Unbalanced brackets in command");
        }

        // Check for null bytes
        if (command.Contains('\0'))
        {
            return new SyntaxValidationResult(false, "Command contains null bytes");
        }

        // Check for control characters (except common whitespace)
        if (command.Any(c => char.IsControl(c) && c != '\t' && c != '\n' && c != '\r'))
        {
            return new SyntaxValidationResult(false, "Command contains invalid control characters");
        }

        return new SyntaxValidationResult(true, "Syntax validation passed");
    }

    private async Task<ParameterValidationResult> DetectParameterInjectionAsync(
        string parameterName,
        string parameterValue,
        CancellationToken cancellationToken)
    {
        // Check for command injection patterns
        var injectionPatterns = new[]
        {
            @"[;&|`$]",                    // Shell metacharacters
            @"\$\([^)]*\)",                 // Command substitution
            @"`[^`]*`",                     // Backtick execution
            @"<\s*script",                  // Script tags
            @"javascript:",                 // JavaScript protocol
            @"on\w+\s*=",                   // Event handlers
            @"\.\./",                       // Path traversal
            @"['\""]\s*OR\s+['\""]\d+['\""]\s*=\s*['\""]\d+", // SQL injection
        };

        foreach (var pattern in injectionPatterns)
        {
            if (Regex.IsMatch(parameterValue, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogWarning(
                    "Potential injection detected in parameter '{ParameterName}' with pattern '{Pattern}'",
                    parameterName,
                    pattern);

                return new ParameterValidationResult(
                    false,
                    $"Potential injection detected in parameter '{parameterName}'");
            }
        }

        await Task.CompletedTask; // Placeholder for async semantic analysis
        return new ParameterValidationResult(true, "No injection detected");
    }

    private List<string> GetWhitelistForRole(AgentRole role, AgentEnvironment environment)
    {
        var whitelist = new List<string>();

        if (_config.RoleWhitelists is null)
        {
            return whitelist;
        }

        // Get role-specific whitelist
        if (_config.RoleWhitelists.TryGetValue(role, out var roleWhitelist))
        {
            if (roleWhitelist.SystemCommands is not null)
                whitelist.AddRange(roleWhitelist.SystemCommands);

            if (roleWhitelist.AzureCommands is not null)
                whitelist.AddRange(roleWhitelist.AzureCommands);

            if (roleWhitelist.FilesystemCommands is not null)
                whitelist.AddRange(roleWhitelist.FilesystemCommands);

            // Environment-specific overrides
            if (environment == AgentEnvironment.Production && roleWhitelist.ProductionOverrides is not null)
            {
                // Remove development-only commands
                if (roleWhitelist.ProductionOverrides.RemovedCommands is not null)
                {
                    whitelist.RemoveAll(cmd => roleWhitelist.ProductionOverrides.RemovedCommands.Contains(cmd, StringComparer.OrdinalIgnoreCase));
                }

                // Add production-specific commands
                if (roleWhitelist.ProductionOverrides.AddedCommands is not null)
                {
                    whitelist.AddRange(roleWhitelist.ProductionOverrides.AddedCommands);
                }
            }
        }

        return whitelist;
    }

    private AllowedParameters GetAllowedParameters(string command, AgentRole role)
    {
        var commandName = ExtractCommandName(command);

        // Default allowed parameters
        var allowedParams = new AllowedParameters
        {
            ParameterNames = new List<string>(),
            ValuePatterns = new Dictionary<string, string>()
        };

        if (_config.ParameterRules is null)
        {
            return allowedParams;
        }

        // Check for command-specific parameter rules
        var key = $"{role}:{commandName}";
        if (_config.ParameterRules.TryGetValue(key, out var paramRules))
        {
            return paramRules;
        }

        // Check for role-level parameter rules
        if (_config.ParameterRules.TryGetValue(role.ToString(), out paramRules))
        {
            return paramRules;
        }

        return allowedParams;
    }

    private bool DetermineApprovalRequirement(AgentContext context, string command, Dictionary<string, string>? parameters)
    {
        // Manual mode always requires approval
        if (context.Role == AgentRole.Manual)
        {
            return true;
        }

        // Check if command requires approval based on configuration
        var commandName = ExtractCommandName(command);

        // Write operations in Limited Write mode require approval
        if (context.Role == AgentRole.LimitedWrite && IsWriteOperation(commandName))
        {
            return true;
        }

        // Supervised mode requires approval for destructive operations
        if (context.Role == AgentRole.Supervised && IsDestructiveOperation(commandName))
        {
            return true;
        }

        // Production environment requires additional approval
        if (context.Environment == AgentEnvironment.Production && IsHighRiskOperation(commandName))
        {
            return true;
        }

        return false;
    }

    private static string ExtractCommandName(string command)
    {
        var parts = command.TrimStart().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : command;
    }

    private static bool IsWriteOperation(string commandName)
    {
        var writeVerbs = new[] { "Set-", "New-", "Add-", "Update-", "Write-", "Out-", "Export-" };
        return writeVerbs.Any(verb => commandName.StartsWith(verb, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDestructiveOperation(string commandName)
    {
        var destructiveVerbs = new[] { "Remove-", "Delete-", "Clear-", "Stop-", "Disable-", "Format-", "Uninstall-" };
        return destructiveVerbs.Any(verb => commandName.StartsWith(verb, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsHighRiskOperation(string commandName)
    {
        var highRiskCommands = new[]
        {
            "Restart-Service",
            "Stop-Service",
            "Restart-Computer",
            "Stop-Computer",
            "Remove-Item",
            "Format-Volume",
            "Clear-EventLog",
            "Disable-NetAdapter"
        };

        return highRiskCommands.Any(cmd => commandName.Equals(cmd, StringComparison.OrdinalIgnoreCase));
    }

    private CommandFilterResult CreateFilterResult(
        bool isAllowed,
        FilterReason reason,
        string? message,
        AgentContext context,
        string command,
        bool requiresApproval = false)
    {
        return new CommandFilterResult
        {
            IsAllowed = isAllowed,
            Reason = reason,
            Message = message,
            RequiresApproval = requiresApproval,
            AgentId = context.AgentId,
            SessionId = context.SessionId,
            Command = command,
            Role = context.Role,
            Environment = context.Environment,
            Timestamp = DateTime.UtcNow,
            ValidationLayers = _config.ValidationLayers ?? new List<string>()
        };
    }

    private static Dictionary<string, Regex> CompilePatterns(IEnumerable<string> patterns)
    {
        var compiled = new Dictionary<string, Regex>();

        foreach (var pattern in patterns)
        {
            try
            {
                compiled[pattern] = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }
            catch (ArgumentException)
            {
                // Skip invalid regex patterns
            }
        }

        return compiled;
    }

    #endregion
}

#region Supporting Interfaces

/// <summary>
/// Interface for semantic command analysis
/// </summary>
public interface ISemanticAnalyzer
{
    Task<SemanticAnalysisResult> AnalyzeCommandAsync(
        string command,
        Dictionary<string, string>? parameters,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for context validation
/// </summary>
public interface IContextValidator
{
    Task<ContextValidationResult> ValidateContextAsync(
        AgentContext context,
        string command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for rate limiting
/// </summary>
public interface IRateLimiter
{
    Task<RateLimitResult> CheckRateLimitAsync(
        AgentContext context,
        CancellationToken cancellationToken = default);
}

#endregion

#region Result Classes

public sealed record SyntaxValidationResult(bool IsValid, string? Reason);

public sealed record SemanticAnalysisResult(bool IsAllowed, string? Reason);

public sealed record ContextValidationResult(bool IsValid, string? Reason);

public sealed record RateLimitResult(bool IsAllowed, int RemainingRequests = 0, TimeSpan? RetryAfter = null);

public sealed record ParameterValidationResult(bool IsValid, string? Reason);

#endregion
