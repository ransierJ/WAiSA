namespace WAiSA.API.Security.Models;

/// <summary>
/// Result of command filtering with detailed decision information
/// </summary>
public sealed class CommandFilterResult
{
    /// <summary>
    /// Indicates if the command is allowed to execute
    /// </summary>
    public required bool IsAllowed { get; init; }

    /// <summary>
    /// Reason for the filtering decision
    /// </summary>
    public required FilterReason Reason { get; init; }

    /// <summary>
    /// Human-readable message explaining the decision
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Indicates if human approval is required before execution
    /// </summary>
    public bool RequiresApproval { get; init; }

    /// <summary>
    /// Agent identifier that attempted the command
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Session identifier for tracking
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// The command that was filtered
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Agent role at time of filtering
    /// </summary>
    public required AgentRole Role { get; init; }

    /// <summary>
    /// Environment at time of filtering
    /// </summary>
    public required AgentEnvironment Environment { get; init; }

    /// <summary>
    /// Timestamp when filtering occurred
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Validation layers that were executed
    /// </summary>
    public List<string>? ValidationLayers { get; init; }

    /// <summary>
    /// Detailed validation results for each layer
    /// </summary>
    public Dictionary<string, object>? LayerResults { get; init; }

    /// <summary>
    /// Risk score (0-100) if calculated
    /// </summary>
    public int? RiskScore { get; init; }

    /// <summary>
    /// Recommended actions for the user/admin
    /// </summary>
    public List<string>? RecommendedActions { get; init; }

    /// <summary>
    /// Alternative safe commands that could achieve similar results
    /// </summary>
    public List<string>? Alternatives { get; init; }

    /// <summary>
    /// Unique identifier for this filtering decision (for audit trail)
    /// </summary>
    public string DecisionId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Create a successful filter result
    /// </summary>
    public static CommandFilterResult Allow(AgentContext context, string command, bool requiresApproval = false)
    {
        return new CommandFilterResult
        {
            IsAllowed = true,
            Reason = FilterReason.Allowed,
            Message = "Command passed all validation layers",
            RequiresApproval = requiresApproval,
            AgentId = context.AgentId,
            SessionId = context.SessionId,
            Command = command,
            Role = context.Role,
            Environment = context.Environment
        };
    }

    /// <summary>
    /// Create a blocked filter result
    /// </summary>
    public static CommandFilterResult Deny(AgentContext context, string command, FilterReason reason, string message)
    {
        return new CommandFilterResult
        {
            IsAllowed = false,
            Reason = reason,
            Message = message,
            RequiresApproval = false,
            AgentId = context.AgentId,
            SessionId = context.SessionId,
            Command = command,
            Role = context.Role,
            Environment = context.Environment
        };
    }

    public override string ToString()
    {
        return $"FilterResult[{DecisionId}] IsAllowed={IsAllowed} Reason={Reason} RequiresApproval={RequiresApproval}";
    }
}

/// <summary>
/// Reasons for command filtering decisions
/// </summary>
public enum FilterReason
{
    /// <summary>
    /// Command is allowed to execute
    /// </summary>
    Allowed = 0,

    /// <summary>
    /// Command matched a blacklist pattern
    /// </summary>
    Blacklisted = 1,

    /// <summary>
    /// Command is not in the role-based whitelist
    /// </summary>
    NotWhitelisted = 2,

    /// <summary>
    /// Command has invalid syntax or structure
    /// </summary>
    InvalidSyntax = 3,

    /// <summary>
    /// Command parameters are invalid or dangerous
    /// </summary>
    InvalidParameters = 4,

    /// <summary>
    /// Semantic analysis detected potentially dangerous intent
    /// </summary>
    SemanticViolation = 5,

    /// <summary>
    /// Command violates context-specific rules
    /// </summary>
    ContextViolation = 6,

    /// <summary>
    /// Rate limit has been exceeded
    /// </summary>
    RateLimitExceeded = 7,

    /// <summary>
    /// Agent lacks necessary permissions
    /// </summary>
    InsufficientPermissions = 8,

    /// <summary>
    /// Command requires approval that has not been granted
    /// </summary>
    ApprovalRequired = 9,

    /// <summary>
    /// Internal error during filtering process
    /// </summary>
    InternalError = 10,

    /// <summary>
    /// Command is blocked by circuit breaker
    /// </summary>
    CircuitBreakerOpen = 11,

    /// <summary>
    /// Command detected as potential privilege escalation
    /// </summary>
    PrivilegeEscalation = 12,

    /// <summary>
    /// Command detected as potential lateral movement
    /// </summary>
    LateralMovement = 13,

    /// <summary>
    /// Command detected as potential data exfiltration
    /// </summary>
    DataExfiltration = 14,

    /// <summary>
    /// Command uses obfuscation or encoding
    /// </summary>
    ObfuscationDetected = 15
}
