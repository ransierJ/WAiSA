using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WAiSA.API.Security.Guards;

/// <summary>
/// Represents the type of lateral movement violation detected.
/// </summary>
public enum ViolationType
{
    /// <summary>
    /// A blocked remote cmdlet was detected (e.g., Enter-PSSession, Invoke-Command).
    /// </summary>
    RemoteCmdlet,

    /// <summary>
    /// A remote parameter targeting non-localhost systems was detected.
    /// </summary>
    RemoteParameter,

    /// <summary>
    /// Network access to restricted internal networks was detected.
    /// </summary>
    NetworkRestriction,

    /// <summary>
    /// WinRM or SSH protocol usage was detected.
    /// </summary>
    RemoteProtocol
}

/// <summary>
/// Represents the result of a lateral movement validation check.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the command is allowed to execute.
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Gets or sets the reason why the command was blocked, if applicable.
    /// </summary>
    public string? BlockedReason { get; set; }

    /// <summary>
    /// Gets or sets the type of violation detected, if applicable.
    /// </summary>
    public ViolationType? ViolationType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agent should be quarantined due to this violation.
    /// </summary>
    public bool ShouldQuarantine { get; set; }

    /// <summary>
    /// Gets or sets additional context about the violation.
    /// </summary>
    public Dictionary<string, string> Context { get; set; } = new();
}

/// <summary>
/// Configuration model for lateral movement prevention settings.
/// </summary>
internal sealed class LateralMovementConfig
{
    public bool Enabled { get; set; }
    public bool BlockRemoteExecution { get; set; }
    public List<string> BlockedCmdlets { get; set; } = new();
    public List<string> AllowedTargets { get; set; } = new();
    public NetworkRestrictionsConfig NetworkRestrictions { get; set; } = new();
    public ViolationHandlingConfig OnViolation { get; set; } = new();
}

/// <summary>
/// Configuration model for network restrictions.
/// </summary>
internal sealed class NetworkRestrictionsConfig
{
    public bool DenyOutboundToInternalNetworks { get; set; }
    public List<string> AllowedAzureServices { get; set; } = new();
    public List<int> BlockedPorts { get; set; } = new();
}

/// <summary>
/// Configuration model for violation handling behavior.
/// </summary>
internal sealed class ViolationHandlingConfig
{
    public string Action { get; set; } = "block";
    public bool NotifySecurityTeam { get; set; }
    public bool QuarantineAgent { get; set; }
}

/// <summary>
/// Root configuration model for the YAML configuration file.
/// </summary>
internal sealed class GuardrailsConfig
{
    public AgentSecurityConfig AgentSecurity { get; set; } = new();
}

/// <summary>
/// Agent security configuration model.
/// </summary>
internal sealed class AgentSecurityConfig
{
    public LateralMovementConfig LateralMovement { get; set; } = new();
}

/// <summary>
/// Interface for lateral movement guard functionality.
/// Provides contract for dependency injection and testing.
/// </summary>
public interface ILateralMovementGuard
{
    /// <summary>
    /// Validates a command to ensure it does not attempt lateral movement.
    /// </summary>
    /// <param name="command">The command to validate.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A validation result indicating whether the command is allowed.</returns>
    Task<ValidationResult> ValidateCommandAsync(string command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads the configuration from the YAML file.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A task representing the async operation.</returns>
    Task ReloadConfigurationAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Guards against lateral movement attempts by AI agents.
/// Prevents execution of commands that target remote systems or internal networks.
/// Thread-safe implementation for concurrent validation operations.
/// </summary>
public sealed class LateralMovementGuard : ILateralMovementGuard
{
    private readonly ILogger<LateralMovementGuard> _logger;
    private readonly string _configPath;
    private readonly SemaphoreSlim _configLock = new(1, 1);

    private LateralMovementConfig _config;

    // Regex patterns compiled for performance
    private static readonly Regex ComputerNamePattern = new(
        @"-ComputerName\s+(?<target>[^\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex WinRsPattern = new(
        @"\bwinrs\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex SshPattern = new(
        @"\bssh\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex IpAddressPattern = new(
        @"\b(?:\d{1,3}\.){3}\d{1,3}\b",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    // Internal network ranges
    private static readonly string[] InternalNetworkPrefixes = new[]
    {
        "10.",
        "172.16.", "172.17.", "172.18.", "172.19.",
        "172.20.", "172.21.", "172.22.", "172.23.",
        "172.24.", "172.25.", "172.26.", "172.27.",
        "172.28.", "172.29.", "172.30.", "172.31.",
        "192.168."
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="LateralMovementGuard"/> class.
    /// </summary>
    /// <param name="logger">Logger for security event logging.</param>
    /// <param name="configPath">Path to the guardrails YAML configuration file.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown when configPath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when configuration file does not exist.</exception>
    public LateralMovementGuard(ILogger<LateralMovementGuard> logger, string configPath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("Configuration path cannot be null or empty.", nameof(configPath));
        }

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found: {configPath}", configPath);
        }

        _configPath = configPath;
        _config = LoadConfiguration();

        _logger.LogInformation(
            "LateralMovementGuard initialized. Enabled: {Enabled}, Blocked cmdlets: {Count}",
            _config.Enabled,
            _config.BlockedCmdlets.Count);
    }

    /// <inheritdoc/>
    public async Task<ValidationResult> ValidateCommandAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return new ValidationResult { IsAllowed = true };
        }

        try
        {
            // Acquire read access to configuration
            await _configLock.WaitAsync(cancellationToken);
            try
            {
                if (!_config.Enabled)
                {
                    _logger.LogDebug("Lateral movement guard is disabled, allowing command");
                    return new ValidationResult { IsAllowed = true };
                }

                // Check for blocked remote cmdlets
                var cmdletCheck = CheckBlockedCmdlets(command);
                if (!cmdletCheck.IsAllowed)
                {
                    return cmdletCheck;
                }

                // Check for remote computer name parameters
                var parameterCheck = CheckRemoteParameters(command);
                if (!parameterCheck.IsAllowed)
                {
                    return parameterCheck;
                }

                // Check for WinRM/SSH usage
                var protocolCheck = CheckRemoteProtocols(command);
                if (!protocolCheck.IsAllowed)
                {
                    return protocolCheck;
                }

                // Check for internal network access
                var networkCheck = CheckNetworkRestrictions(command);
                if (!networkCheck.IsAllowed)
                {
                    return networkCheck;
                }

                _logger.LogDebug("Command passed lateral movement validation: {Command}",
                    TruncateForLogging(command));

                return new ValidationResult { IsAllowed = true };
            }
            finally
            {
                _configLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Validation cancelled for command: {Command}",
                TruncateForLogging(command));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during lateral movement validation");

            // Fail secure: deny on error
            return new ValidationResult
            {
                IsAllowed = false,
                BlockedReason = "Internal validation error occurred",
                ShouldQuarantine = false,
                Context = new Dictionary<string, string>
                {
                    ["Error"] = ex.Message
                }
            };
        }
    }

    /// <inheritdoc/>
    public async Task ReloadConfigurationAsync(CancellationToken cancellationToken = default)
    {
        await _configLock.WaitAsync(cancellationToken);
        try
        {
            _config = LoadConfiguration();
            _logger.LogInformation("Configuration reloaded successfully from {Path}", _configPath);
        }
        finally
        {
            _configLock.Release();
        }
    }

    /// <summary>
    /// Checks if the command contains blocked remote execution cmdlets.
    /// </summary>
    private ValidationResult CheckBlockedCmdlets(string command)
    {
        if (!_config.BlockRemoteExecution)
        {
            return new ValidationResult { IsAllowed = true };
        }

        foreach (var blockedCmdlet in _config.BlockedCmdlets)
        {
            // Case-insensitive match for cmdlets
            if (command.Contains(blockedCmdlet, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Blocked remote cmdlet detected: {Cmdlet} in command: {Command}",
                    blockedCmdlet,
                    TruncateForLogging(command));

                return new ValidationResult
                {
                    IsAllowed = false,
                    BlockedReason = $"Remote execution cmdlet '{blockedCmdlet}' is not permitted",
                    ViolationType = Guards.ViolationType.RemoteCmdlet,
                    ShouldQuarantine = _config.OnViolation.QuarantineAgent,
                    Context = new Dictionary<string, string>
                    {
                        ["BlockedCmdlet"] = blockedCmdlet,
                        ["DetectedIn"] = TruncateForLogging(command)
                    }
                };
            }
        }

        return new ValidationResult { IsAllowed = true };
    }

    /// <summary>
    /// Checks if the command contains -ComputerName parameter targeting remote systems.
    /// </summary>
    private ValidationResult CheckRemoteParameters(string command)
    {
        var match = ComputerNamePattern.Match(command);
        if (!match.Success)
        {
            return new ValidationResult { IsAllowed = true };
        }

        var targetComputer = match.Groups["target"].Value.Trim('"', '\'');

        // Check if target is in allowed list (localhost, 127.0.0.1, ., $env:COMPUTERNAME)
        foreach (var allowedTarget in _config.AllowedTargets)
        {
            if (targetComputer.Equals(allowedTarget, StringComparison.OrdinalIgnoreCase) ||
                targetComputer.Equals(allowedTarget.Replace("$env:COMPUTERNAME", Environment.MachineName),
                    StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult { IsAllowed = true };
            }
        }

        _logger.LogWarning(
            "Remote computer name parameter detected: -ComputerName {Target} in command: {Command}",
            targetComputer,
            TruncateForLogging(command));

        return new ValidationResult
        {
            IsAllowed = false,
            BlockedReason = $"Remote computer name '{targetComputer}' is not permitted. Only localhost is allowed.",
            ViolationType = Guards.ViolationType.RemoteParameter,
            ShouldQuarantine = _config.OnViolation.QuarantineAgent,
            Context = new Dictionary<string, string>
            {
                ["RemoteTarget"] = targetComputer,
                ["AllowedTargets"] = string.Join(", ", _config.AllowedTargets)
            }
        };
    }

    /// <summary>
    /// Checks if the command uses WinRM or SSH protocols.
    /// </summary>
    private ValidationResult CheckRemoteProtocols(string command)
    {
        // Check for winrs (Windows Remote Shell)
        if (WinRsPattern.IsMatch(command))
        {
            _logger.LogWarning("WinRM usage detected in command: {Command}",
                TruncateForLogging(command));

            return new ValidationResult
            {
                IsAllowed = false,
                BlockedReason = "WinRM (winrs) usage is not permitted",
                ViolationType = Guards.ViolationType.RemoteProtocol,
                ShouldQuarantine = _config.OnViolation.QuarantineAgent,
                Context = new Dictionary<string, string>
                {
                    ["Protocol"] = "WinRM",
                    ["DetectedIn"] = TruncateForLogging(command)
                }
            };
        }

        // Check for SSH
        if (SshPattern.IsMatch(command))
        {
            _logger.LogWarning("SSH usage detected in command: {Command}",
                TruncateForLogging(command));

            return new ValidationResult
            {
                IsAllowed = false,
                BlockedReason = "SSH usage is not permitted",
                ViolationType = Guards.ViolationType.RemoteProtocol,
                ShouldQuarantine = _config.OnViolation.QuarantineAgent,
                Context = new Dictionary<string, string>
                {
                    ["Protocol"] = "SSH",
                    ["DetectedIn"] = TruncateForLogging(command)
                }
            };
        }

        return new ValidationResult { IsAllowed = true };
    }

    /// <summary>
    /// Checks if the command attempts to access internal network ranges.
    /// </summary>
    private ValidationResult CheckNetworkRestrictions(string command)
    {
        if (!_config.NetworkRestrictions.DenyOutboundToInternalNetworks)
        {
            return new ValidationResult { IsAllowed = true };
        }

        // Extract all IP addresses from the command
        var ipMatches = IpAddressPattern.Matches(command);

        foreach (Match match in ipMatches)
        {
            var ipAddress = match.Value;

            // Check if IP is in internal network ranges
            foreach (var prefix in InternalNetworkPrefixes)
            {
                if (ipAddress.StartsWith(prefix, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "Internal network access detected: {IpAddress} in command: {Command}",
                        ipAddress,
                        TruncateForLogging(command));

                    return new ValidationResult
                    {
                        IsAllowed = false,
                        BlockedReason = $"Access to internal network address '{ipAddress}' is not permitted",
                        ViolationType = Guards.ViolationType.NetworkRestriction,
                        ShouldQuarantine = _config.OnViolation.QuarantineAgent,
                        Context = new Dictionary<string, string>
                        {
                            ["BlockedIpAddress"] = ipAddress,
                            ["NetworkRange"] = prefix + "x.x",
                            ["DetectedIn"] = TruncateForLogging(command)
                        }
                    };
                }
            }
        }

        return new ValidationResult { IsAllowed = true };
    }

    /// <summary>
    /// Loads configuration from the YAML file.
    /// </summary>
    /// <returns>The lateral movement configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration cannot be loaded.</exception>
    private LateralMovementConfig LoadConfiguration()
    {
        try
        {
            var yaml = File.ReadAllText(_configPath);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var config = deserializer.Deserialize<GuardrailsConfig>(yaml);

            if (config?.AgentSecurity?.LateralMovement == null)
            {
                throw new InvalidOperationException(
                    "Configuration file is missing 'agent_security.lateral_movement' section");
            }

            return config.AgentSecurity.LateralMovement;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to load configuration from {Path}", _configPath);
            throw new InvalidOperationException(
                $"Failed to load lateral movement configuration from {_configPath}", ex);
        }
    }

    /// <summary>
    /// Truncates command text for safe logging.
    /// </summary>
    private static string TruncateForLogging(string command, int maxLength = 200)
    {
        if (command.Length <= maxLength)
        {
            return command;
        }

        return command.Substring(0, maxLength) + "...";
    }
}
