using System.Text.Json.Serialization;

namespace WAiSA.API.Security.Auditing;

/// <summary>
/// Represents a complete audit log entry with all required fields
/// </summary>
public class AuditLogEntry
{
    /// <summary>
    /// Timestamp when the event occurred (UTC)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Unique event identifier
    /// </summary>
    [JsonPropertyName("event_id")]
    public required string EventId { get; init; }

    /// <summary>
    /// Agent identifier
    /// </summary>
    [JsonPropertyName("agent_id")]
    public required string AgentId { get; init; }

    /// <summary>
    /// Session identifier
    /// </summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// User identifier
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// Event type
    /// </summary>
    [JsonPropertyName("event_type")]
    public required string EventType { get; init; }

    /// <summary>
    /// Severity level
    /// </summary>
    [JsonPropertyName("severity")]
    public required string Severity { get; init; }

    /// <summary>
    /// Event data containing command, parameters, and result
    /// </summary>
    [JsonPropertyName("event_data")]
    public required EventData EventData { get; init; }

    /// <summary>
    /// Security context information
    /// </summary>
    [JsonPropertyName("security_context")]
    public required SecurityContext SecurityContext { get; init; }

    /// <summary>
    /// Resource context for Azure resources
    /// </summary>
    [JsonPropertyName("resource_context")]
    public ResourceContext? ResourceContext { get; init; }

    /// <summary>
    /// SHA256 integrity hash of the log entry
    /// </summary>
    [JsonPropertyName("integrity_hash")]
    public required string IntegrityHash { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Contains the core event data
/// </summary>
public class EventData
{
    /// <summary>
    /// Command or action executed
    /// </summary>
    [JsonPropertyName("command")]
    public required string Command { get; init; }

    /// <summary>
    /// Sanitized parameters
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; init; }

    /// <summary>
    /// Result or outcome
    /// </summary>
    [JsonPropertyName("result")]
    public string? Result { get; init; }

    /// <summary>
    /// Execution time in milliseconds
    /// </summary>
    [JsonPropertyName("execution_time_ms")]
    public long? ExecutionTimeMs { get; init; }

    /// <summary>
    /// Error message if applicable
    /// </summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Stack trace if error occurred
    /// </summary>
    [JsonPropertyName("stack_trace")]
    public string? StackTrace { get; init; }
}

/// <summary>
/// Contains security-related context
/// </summary>
public class SecurityContext
{
    /// <summary>
    /// Source IP address
    /// </summary>
    [JsonPropertyName("source_ip")]
    public string? SourceIpAddress { get; init; }

    /// <summary>
    /// Authentication method
    /// </summary>
    [JsonPropertyName("auth_method")]
    public string? AuthenticationMethod { get; init; }

    /// <summary>
    /// Authorization decision
    /// </summary>
    [JsonPropertyName("authorization_decision")]
    public string? AuthorizationDecision { get; init; }
}

/// <summary>
/// Contains Azure resource context
/// </summary>
public class ResourceContext
{
    /// <summary>
    /// Azure subscription ID
    /// </summary>
    [JsonPropertyName("subscription_id")]
    public string? SubscriptionId { get; init; }

    /// <summary>
    /// Azure resource group
    /// </summary>
    [JsonPropertyName("resource_group")]
    public string? ResourceGroup { get; init; }

    /// <summary>
    /// Azure resource ID
    /// </summary>
    [JsonPropertyName("resource_id")]
    public string? ResourceId { get; init; }
}
