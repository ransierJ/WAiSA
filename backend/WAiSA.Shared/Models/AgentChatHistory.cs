using System.Text.Json.Serialization;

namespace WAiSA.Shared.Models;

/// <summary>
/// Represents a chat message in an agent-specific conversation
/// Stored permanently for audit purposes (no TTL)
/// </summary>
public class AgentChatHistory
{
    /// <summary>
    /// Unique identifier (Cosmos DB document ID)
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Agent ID (partition key for efficient querying)
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Session ID for grouping related conversations
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Message role (user or assistant)
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Commands executed during this message (if any)
    /// </summary>
    public List<ExecutedCommand> Commands { get; set; } = new();

    /// <summary>
    /// Activity logs for this message
    /// </summary>
    public List<ActivityLogEntry> ActivityLogs { get; set; } = new();

    /// <summary>
    /// Tokens used for this message
    /// </summary>
    public int? TokensUsed { get; set; }

    /// <summary>
    /// Cascade metadata (search stage info)
    /// </summary>
    public Dictionary<string, object>? CascadeMetadata { get; set; }

    /// <summary>
    /// Timestamp of the message
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who initiated the chat (for audit)
    /// </summary>
    public string? InitiatedBy { get; set; }

    /// <summary>
    /// IP address of the request (for audit)
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Whether this was a successful interaction
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if interaction failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents an activity log entry for a chat message
/// </summary>
public class ActivityLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Details { get; set; }
}
