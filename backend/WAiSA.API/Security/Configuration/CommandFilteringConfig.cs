using WAiSA.API.Security.Models;

namespace WAiSA.API.Security.Configuration;

/// <summary>
/// Configuration for command filtering engine
/// Binds to ai-agent-guardrails-enhanced.yml
/// </summary>
public sealed class CommandFilteringConfig
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "agent_security:command_filtering";

    /// <summary>
    /// Enable or disable command filtering
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Description of the filtering configuration
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Filtering strategy: whitelist-first, blacklist-first, hybrid
    /// </summary>
    public string Strategy { get; set; } = "whitelist-first";

    /// <summary>
    /// Ordered list of validation layers to execute
    /// </summary>
    public List<string>? ValidationLayers { get; set; }

    /// <summary>
    /// Input validation constraints
    /// </summary>
    public InputConstraints? InputConstraints { get; set; }

    /// <summary>
    /// Blacklist configuration
    /// </summary>
    public BlacklistConfig? Blacklist { get; set; }

    /// <summary>
    /// Role-based whitelist configurations
    /// </summary>
    public Dictionary<AgentRole, RoleWhitelist>? RoleWhitelists { get; set; }

    /// <summary>
    /// Parameter validation rules by role and command
    /// </summary>
    public Dictionary<string, AllowedParameters>? ParameterRules { get; set; }

    /// <summary>
    /// Dynamic whitelist configuration
    /// </summary>
    public DynamicWhitelistConfig? DynamicWhitelist { get; set; }

    /// <summary>
    /// Validate configuration settings
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (ValidationLayers is null || ValidationLayers.Count == 0)
        {
            errors.Add("ValidationLayers must contain at least one layer");
        }

        if (string.IsNullOrWhiteSpace(Strategy))
        {
            errors.Add("Strategy must be specified");
        }

        if (InputConstraints is not null)
        {
            if (InputConstraints.MaxCommandLength <= 0)
            {
                errors.Add("MaxCommandLength must be positive");
            }

            if (InputConstraints.MaxParameters <= 0)
            {
                errors.Add("MaxParameters must be positive");
            }
        }

        return errors.Count == 0;
    }
}

/// <summary>
/// Input validation constraints
/// </summary>
public sealed class InputConstraints
{
    /// <summary>
    /// Maximum allowed command length
    /// </summary>
    public int MaxCommandLength { get; set; } = 10000;

    /// <summary>
    /// Maximum number of parameters
    /// </summary>
    public int MaxParameters { get; set; } = 50;

    /// <summary>
    /// Maximum length of a single parameter
    /// </summary>
    public int MaxParameterLength { get; set; } = 1000;

    /// <summary>
    /// Maximum nesting depth for complex commands
    /// </summary>
    public int MaxNestingDepth { get; set; } = 5;

    /// <summary>
    /// Allow PowerShell aliases (e.g., "dir" instead of "Get-ChildItem")
    /// </summary>
    public bool AllowAliases { get; set; } = false;

    /// <summary>
    /// Require full cmdlet names (no abbreviations)
    /// </summary>
    public bool RequireFullCmdletNames { get; set; } = true;
}

/// <summary>
/// Blacklist configuration for dangerous patterns
/// </summary>
public sealed class BlacklistConfig
{
    /// <summary>
    /// Enable blacklist checking
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Regular expression patterns to block
    /// </summary>
    public List<string>? Patterns { get; set; }

    /// <summary>
    /// Additional blocked command names
    /// </summary>
    public List<string>? BlockedCommands { get; set; }

    /// <summary>
    /// Action to take when blacklist match found
    /// </summary>
    public string Action { get; set; } = "block";

    /// <summary>
    /// Alert security team on blacklist violations
    /// </summary>
    public bool AlertOnViolation { get; set; } = true;
}

/// <summary>
/// Role-based whitelist configuration
/// </summary>
public sealed class RoleWhitelist
{
    /// <summary>
    /// System/PowerShell commands allowed for this role
    /// </summary>
    public List<string>? SystemCommands { get; set; }

    /// <summary>
    /// Azure cmdlets allowed for this role
    /// </summary>
    public List<string>? AzureCommands { get; set; }

    /// <summary>
    /// Filesystem commands allowed for this role
    /// </summary>
    public List<string>? FilesystemCommands { get; set; }

    /// <summary>
    /// Monitoring and diagnostic commands
    /// </summary>
    public List<string>? MonitoringCommands { get; set; }

    /// <summary>
    /// Production environment overrides
    /// </summary>
    public EnvironmentOverrides? ProductionOverrides { get; set; }

    /// <summary>
    /// Development environment overrides
    /// </summary>
    public EnvironmentOverrides? DevelopmentOverrides { get; set; }
}

/// <summary>
/// Environment-specific whitelist overrides
/// </summary>
public sealed class EnvironmentOverrides
{
    /// <summary>
    /// Commands to add for this environment
    /// </summary>
    public List<string>? AddedCommands { get; set; }

    /// <summary>
    /// Commands to remove for this environment
    /// </summary>
    public List<string>? RemovedCommands { get; set; }

    /// <summary>
    /// Additional restrictions for this environment
    /// </summary>
    public List<string>? AdditionalRestrictions { get; set; }
}

/// <summary>
/// Allowed parameters configuration for a command
/// </summary>
public sealed class AllowedParameters
{
    /// <summary>
    /// List of allowed parameter names
    /// </summary>
    public List<string>? ParameterNames { get; set; }

    /// <summary>
    /// Regex patterns for parameter values (key = parameter name, value = regex pattern)
    /// </summary>
    public Dictionary<string, string>? ValuePatterns { get; set; }

    /// <summary>
    /// Allowed literal values for specific parameters
    /// </summary>
    public Dictionary<string, List<string>>? AllowedValues { get; set; }

    /// <summary>
    /// Parameters that are required for this command
    /// </summary>
    public List<string>? RequiredParameters { get; set; }

    /// <summary>
    /// Parameters that are forbidden for this command
    /// </summary>
    public List<string>? ForbiddenParameters { get; set; }
}

/// <summary>
/// Dynamic whitelist configuration
/// </summary>
public sealed class DynamicWhitelistConfig
{
    /// <summary>
    /// Enable dynamic whitelist expansion
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Require approval for dynamic whitelist additions
    /// </summary>
    public bool RequireApproval { get; set; } = true;

    /// <summary>
    /// Maximum duration for dynamic whitelist entries (hours)
    /// </summary>
    public int MaxDurationHours { get; set; } = 24;

    /// <summary>
    /// Audit all whitelist expansions
    /// </summary>
    public bool AuditExpansions { get; set; } = true;

    /// <summary>
    /// Maximum number of dynamic whitelist entries per agent
    /// </summary>
    public int MaxEntriesPerAgent { get; set; } = 10;
}

/// <summary>
/// Configuration builder for fluent API
/// </summary>
public sealed class CommandFilteringConfigBuilder
{
    private readonly CommandFilteringConfig _config = new();

    public CommandFilteringConfigBuilder EnableFiltering(bool enabled = true)
    {
        _config.Enabled = enabled;
        return this;
    }

    public CommandFilteringConfigBuilder WithStrategy(string strategy)
    {
        _config.Strategy = strategy;
        return this;
    }

    public CommandFilteringConfigBuilder WithValidationLayers(params string[] layers)
    {
        _config.ValidationLayers = new List<string>(layers);
        return this;
    }

    public CommandFilteringConfigBuilder WithInputConstraints(Action<InputConstraints> configure)
    {
        var constraints = new InputConstraints();
        configure(constraints);
        _config.InputConstraints = constraints;
        return this;
    }

    public CommandFilteringConfigBuilder WithBlacklist(Action<BlacklistConfig> configure)
    {
        var blacklist = new BlacklistConfig();
        configure(blacklist);
        _config.Blacklist = blacklist;
        return this;
    }

    public CommandFilteringConfigBuilder AddRoleWhitelist(AgentRole role, Action<RoleWhitelist> configure)
    {
        _config.RoleWhitelists ??= new Dictionary<AgentRole, RoleWhitelist>();

        var whitelist = new RoleWhitelist();
        configure(whitelist);
        _config.RoleWhitelists[role] = whitelist;
        return this;
    }

    public CommandFilteringConfig Build()
    {
        if (!_config.IsValid(out var errors))
        {
            throw new InvalidOperationException($"Invalid configuration: {string.Join(", ", errors)}");
        }

        return _config;
    }
}
