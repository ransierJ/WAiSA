using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using WAiSA.API.Security.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WAiSA.API.Security.Configuration;

/// <summary>
/// Loads and parses ai-agent-guardrails-enhanced.yml configuration
/// </summary>
public sealed class GuardrailsConfigurationLoader
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GuardrailsConfigurationLoader> _logger;
    private readonly string _configPath;

    public GuardrailsConfigurationLoader(
        IConfiguration configuration,
        ILogger<GuardrailsConfigurationLoader> logger,
        string? configPath = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configPath = configPath ?? "ai-agent-guardrails-enhanced.yml";
    }

    /// <summary>
    /// Load command filtering configuration from YAML
    /// </summary>
    public CommandFilteringConfig LoadCommandFilteringConfig()
    {
        try
        {
            var yaml = File.ReadAllText(_configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yamlConfig = deserializer.Deserialize<Dictionary<string, object>>(yaml);

            if (!yamlConfig.TryGetValue("agent_security", out var agentSecurityObj))
            {
                throw new InvalidOperationException("agent_security section not found in YAML");
            }

            var agentSecurity = agentSecurityObj as Dictionary<object, object>
                ?? throw new InvalidOperationException("agent_security is not a valid dictionary");

            if (!agentSecurity.TryGetValue("command_filtering", out var commandFilteringObj))
            {
                throw new InvalidOperationException("command_filtering section not found");
            }

            var commandFilteringData = commandFilteringObj as Dictionary<object, object>
                ?? throw new InvalidOperationException("command_filtering is not a valid dictionary");

            var config = new CommandFilteringConfig
            {
                Enabled = GetValue<bool>(commandFilteringData, "enabled", true),
                Description = GetValue<string>(commandFilteringData, "description"),
                Strategy = GetValue<string>(commandFilteringData, "strategy", "whitelist-first"),
                ValidationLayers = GetListValue<string>(commandFilteringData, "validation_layers"),
                InputConstraints = LoadInputConstraints(commandFilteringData),
                Blacklist = LoadBlacklistConfig(commandFilteringData),
                RoleWhitelists = LoadRoleWhitelists(agentSecurity),
                DynamicWhitelist = LoadDynamicWhitelistConfig(commandFilteringData)
            };

            _logger.LogInformation("Successfully loaded command filtering configuration from {ConfigPath}", _configPath);

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load command filtering configuration from {ConfigPath}", _configPath);
            throw;
        }
    }

    private InputConstraints? LoadInputConstraints(Dictionary<object, object> data)
    {
        if (!data.TryGetValue("input_constraints", out var constraintsObj))
        {
            return null;
        }

        var constraintsData = constraintsObj as Dictionary<object, object>;
        if (constraintsData is null)
        {
            return null;
        }

        return new InputConstraints
        {
            MaxCommandLength = GetValue<int>(constraintsData, "max_command_length", 10000),
            MaxParameters = GetValue<int>(constraintsData, "max_parameters", 50),
            MaxParameterLength = GetValue<int>(constraintsData, "max_parameter_length", 1000),
            MaxNestingDepth = GetValue<int>(constraintsData, "max_nesting_depth", 5),
            AllowAliases = GetValue<bool>(constraintsData, "allow_aliases", false),
            RequireFullCmdletNames = GetValue<bool>(constraintsData, "require_full_cmdlet_names", true)
        };
    }

    private BlacklistConfig? LoadBlacklistConfig(Dictionary<object, object> data)
    {
        if (!data.TryGetValue("blacklist", out var blacklistObj))
        {
            return null;
        }

        var blacklistData = blacklistObj as Dictionary<object, object>;
        if (blacklistData is null)
        {
            return null;
        }

        return new BlacklistConfig
        {
            Enabled = GetValue<bool>(blacklistData, "enabled", true),
            Patterns = GetListValue<string>(blacklistData, "patterns"),
            AlertOnViolation = true
        };
    }

    private DynamicWhitelistConfig? LoadDynamicWhitelistConfig(Dictionary<object, object> data)
    {
        if (!data.TryGetValue("dynamic_whitelist", out var dynamicObj))
        {
            return null;
        }

        var dynamicData = dynamicObj as Dictionary<object, object>;
        if (dynamicData is null)
        {
            return null;
        }

        return new DynamicWhitelistConfig
        {
            Enabled = GetValue<bool>(dynamicData, "enabled", false),
            RequireApproval = GetValue<bool>(dynamicData, "require_approval", true),
            MaxDurationHours = GetValue<int>(dynamicData, "max_duration_hours", 24),
            AuditExpansions = GetValue<bool>(dynamicData, "audit_expansions", true)
        };
    }

    private Dictionary<AgentRole, RoleWhitelist> LoadRoleWhitelists(Dictionary<object, object> agentSecurity)
    {
        var roleWhitelists = new Dictionary<AgentRole, RoleWhitelist>();

        // Load information gathering commands
        if (agentSecurity.TryGetValue("information_gathering", out var infoGatheringObj))
        {
            var infoGathering = infoGatheringObj as Dictionary<object, object>;
            if (infoGathering?.TryGetValue("allowed_cmdlets", out var allowedCmdletsObj) == true)
            {
                var allowedCmdlets = allowedCmdletsObj as Dictionary<object, object>;
                if (allowedCmdlets is not null)
                {
                    var systemCommands = GetListValue<string>(allowedCmdlets, "system");
                    var azureCommands = GetListValue<string>(allowedCmdlets, "azure");
                    var filesystemCommands = GetListValue<string>(allowedCmdlets, "filesystem");
                    var monitoringCommands = GetListValue<string>(allowedCmdlets, "monitoring");

                    // ReadOnly role gets all safe read commands
                    roleWhitelists[AgentRole.ReadOnly] = new RoleWhitelist
                    {
                        SystemCommands = systemCommands,
                        AzureCommands = azureCommands,
                        FilesystemCommands = filesystemCommands,
                        MonitoringCommands = monitoringCommands,
                        ProductionOverrides = new EnvironmentOverrides
                        {
                            RemovedCommands = new List<string> { "Get-Content", "Invoke-*" }
                        }
                    };

                    // Manual role gets same whitelist but requires approval
                    roleWhitelists[AgentRole.Manual] = new RoleWhitelist
                    {
                        SystemCommands = systemCommands,
                        AzureCommands = azureCommands,
                        FilesystemCommands = filesystemCommands,
                        MonitoringCommands = monitoringCommands
                    };
                }
            }
        }

        // Load autonomy tier definitions for other roles
        if (agentSecurity.TryGetValue("autonomy_tiers", out var autonomyTiersObj))
        {
            var autonomyTiers = autonomyTiersObj as Dictionary<object, object>;
            if (autonomyTiers is not null)
            {
                LoadTierWhitelist(autonomyTiers, "tier_2_limited_write", AgentRole.LimitedWrite, roleWhitelists);
                LoadTierWhitelist(autonomyTiers, "tier_3_supervised_automation", AgentRole.Supervised, roleWhitelists);
                LoadTierWhitelist(autonomyTiers, "tier_4_full_autonomy", AgentRole.FullAutonomy, roleWhitelists);
            }
        }

        return roleWhitelists;
    }

    private void LoadTierWhitelist(
        Dictionary<object, object> autonomyTiers,
        string tierKey,
        AgentRole role,
        Dictionary<AgentRole, RoleWhitelist> roleWhitelists)
    {
        if (!autonomyTiers.TryGetValue(tierKey, out var tierObj))
        {
            return;
        }

        var tierData = tierObj as Dictionary<object, object>;
        if (tierData is null)
        {
            return;
        }

        var whitelist = new RoleWhitelist
        {
            SystemCommands = GetListValue<string>(tierData, "auto_approve_commands") ?? new List<string>(),
            AzureCommands = new List<string>(),
            FilesystemCommands = new List<string>()
        };

        // Add additional auto-approve commands if inherits from previous tier
        if (tierData.TryGetValue("additional_auto_approve", out var additionalObj))
        {
            var additional = GetListValue<string>(tierData, "additional_auto_approve");
            if (additional is not null)
            {
                whitelist.SystemCommands.AddRange(additional);
            }
        }

        // If this tier inherits, add commands from parent tier
        if (roleWhitelists.TryGetValue(role - 1, out var parentWhitelist))
        {
            if (parentWhitelist.SystemCommands is not null)
                whitelist.SystemCommands.AddRange(parentWhitelist.SystemCommands);

            if (parentWhitelist.AzureCommands is not null)
                whitelist.AzureCommands.AddRange(parentWhitelist.AzureCommands);

            if (parentWhitelist.FilesystemCommands is not null)
                whitelist.FilesystemCommands.AddRange(parentWhitelist.FilesystemCommands);
        }

        // Production overrides
        var blockedCommands = GetListValue<string>(tierData, "blocked_commands");
        if (blockedCommands is not null && blockedCommands.Count > 0)
        {
            whitelist.ProductionOverrides = new EnvironmentOverrides
            {
                RemovedCommands = blockedCommands
            };
        }

        roleWhitelists[role] = whitelist;
    }

    private T GetValue<T>(Dictionary<object, object> data, string key, T defaultValue = default!)
    {
        if (!data.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        try
        {
            if (value is T typedValue)
            {
                return typedValue;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    private List<T>? GetListValue<T>(Dictionary<object, object> data, string key)
    {
        if (!data.TryGetValue(key, out var value))
        {
            return null;
        }

        if (value is List<object> list)
        {
            return list.Select(item => (T)Convert.ChangeType(item, typeof(T))).ToList();
        }

        return null;
    }
}

/// <summary>
/// Extension methods for registering guardrails configuration
/// </summary>
public static class GuardrailsConfigurationExtensions
{
    /// <summary>
    /// Add guardrails configuration services
    /// </summary>
    public static IServiceCollection AddGuardrailsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        string? configPath = null)
    {
        // Register configuration loader
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<GuardrailsConfigurationLoader>>();
            return new GuardrailsConfigurationLoader(configuration, logger, configPath);
        });

        // Register CommandFilteringConfig as IOptions
        services.AddOptions<CommandFilteringConfig>()
            .Configure<GuardrailsConfigurationLoader>((options, loader) =>
            {
                var config = loader.LoadCommandFilteringConfig();
                options.Enabled = config.Enabled;
                options.Description = config.Description;
                options.Strategy = config.Strategy;
                options.ValidationLayers = config.ValidationLayers;
                options.InputConstraints = config.InputConstraints;
                options.Blacklist = config.Blacklist;
                options.RoleWhitelists = config.RoleWhitelists;
                options.ParameterRules = config.ParameterRules;
                options.DynamicWhitelist = config.DynamicWhitelist;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
