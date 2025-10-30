namespace WAiSA.API.Security.Auditing;

/// <summary>
/// Represents an agent action event to be audited
/// </summary>
public class AgentActionEvent
{
    /// <summary>
    /// Unique identifier for the agent
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Session identifier
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// User identifier who initiated the action
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Type of event
    /// </summary>
    public required EventType EventType { get; init; }

    /// <summary>
    /// Severity level
    /// </summary>
    public required Severity Severity { get; init; }

    /// <summary>
    /// Command or action being performed
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Parameters passed to the command
    /// </summary>
    public Dictionary<string, object>? Parameters { get; init; }

    /// <summary>
    /// Result of the action
    /// </summary>
    public string? Result { get; init; }

    /// <summary>
    /// Execution time in milliseconds
    /// </summary>
    public long? ExecutionTimeMs { get; init; }

    /// <summary>
    /// Source IP address
    /// </summary>
    public string? SourceIpAddress { get; init; }

    /// <summary>
    /// Authentication method used
    /// </summary>
    public string? AuthenticationMethod { get; init; }

    /// <summary>
    /// Authorization decision (Allowed, Denied, etc.)
    /// </summary>
    public string? AuthorizationDecision { get; init; }

    /// <summary>
    /// Azure subscription ID
    /// </summary>
    public string? SubscriptionId { get; init; }

    /// <summary>
    /// Azure resource group
    /// </summary>
    public string? ResourceGroup { get; init; }

    /// <summary>
    /// Azure resource ID
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Error message if action failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Stack trace if error occurred
    /// </summary>
    public string? StackTrace { get; init; }
}
