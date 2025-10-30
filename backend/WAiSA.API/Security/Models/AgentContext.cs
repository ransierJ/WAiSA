namespace WAiSA.API.Security.Models;

/// <summary>
/// Agent execution context containing identity, role, and environment information
/// </summary>
public sealed class AgentContext
{
    /// <summary>
    /// Unique identifier for the agent instance
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Agent role determining autonomy level and permissions
    /// </summary>
    public required AgentRole Role { get; init; }

    /// <summary>
    /// Deployment environment (Development, Production)
    /// </summary>
    public required AgentEnvironment Environment { get; init; }

    /// <summary>
    /// Current session identifier for tracking related operations
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// User identifier associated with this agent
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Tenant or subscription identifier for multi-tenant scenarios
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Additional metadata for context-aware filtering
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Timestamp when the context was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// IP address or hostname of the agent
    /// </summary>
    public string? SourceAddress { get; init; }

    /// <summary>
    /// Validate that the context has all required fields
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(AgentId)
               && !string.IsNullOrWhiteSpace(SessionId)
               && Enum.IsDefined(typeof(AgentRole), Role)
               && Enum.IsDefined(typeof(AgentEnvironment), Environment);
    }

    /// <summary>
    /// Create a context for manual mode
    /// </summary>
    public static AgentContext CreateManual(string agentId, string sessionId, AgentEnvironment environment)
    {
        return new AgentContext
        {
            AgentId = agentId,
            SessionId = sessionId,
            Role = AgentRole.Manual,
            Environment = environment
        };
    }

    /// <summary>
    /// Create a context for read-only mode
    /// </summary>
    public static AgentContext CreateReadOnly(string agentId, string sessionId, AgentEnvironment environment)
    {
        return new AgentContext
        {
            AgentId = agentId,
            SessionId = sessionId,
            Role = AgentRole.ReadOnly,
            Environment = environment
        };
    }

    public override string ToString()
    {
        return $"Agent[{AgentId}] Role={Role} Env={Environment} Session={SessionId}";
    }
}

/// <summary>
/// Agent autonomy levels with progressive trust
/// </summary>
public enum AgentRole
{
    /// <summary>
    /// Tier 0: AI suggests, human reviews and executes
    /// All commands require explicit approval
    /// </summary>
    Manual = 0,

    /// <summary>
    /// Tier 1: Auto-approve safe read-only operations
    /// Only Get-*, Test-*, Measure-* commands allowed
    /// </summary>
    ReadOnly = 1,

    /// <summary>
    /// Tier 2: Read operations + safe service management
    /// Limited write operations with circuit breaker
    /// </summary>
    LimitedWrite = 2,

    /// <summary>
    /// Tier 3: Broader automation with supervision
    /// Configuration changes require approval
    /// Rollback capability required
    /// </summary>
    Supervised = 3,

    /// <summary>
    /// Tier 4: Complete automation (restricted environments only)
    /// Lab/non-production environments only
    /// Complete audit logging and rollback required
    /// </summary>
    FullAutonomy = 4
}

/// <summary>
/// Deployment environment with different security policies
/// </summary>
public enum AgentEnvironment
{
    /// <summary>
    /// Development environment with relaxed restrictions
    /// </summary>
    Development = 0,

    /// <summary>
    /// Production environment with strict security controls
    /// </summary>
    Production = 1,

    /// <summary>
    /// Staging environment for testing
    /// </summary>
    Staging = 2,

    /// <summary>
    /// Isolated/air-gapped environment for full autonomy testing
    /// </summary>
    Isolated = 3
}
