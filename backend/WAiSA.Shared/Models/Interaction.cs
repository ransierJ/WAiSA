using System.Text.Json.Serialization;

namespace WAiSA.Shared.Models;

/// <summary>
/// Represents a single user-AI interaction
/// Stored with 30-day TTL in Cosmos DB
/// </summary>
public class Interaction
{
    /// <summary>
    /// Unique identifier (Cosmos DB document ID)
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Device unique identifier (partition key)
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Conversation identifier for grouping related messages
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// User message/request
    /// </summary>
    public string UserMessage { get; set; } = string.Empty;

    /// <summary>
    /// AI assistant response
    /// </summary>
    public string AssistantResponse { get; set; } = string.Empty;

    /// <summary>
    /// Commands executed during this interaction
    /// </summary>
    public List<ExecutedCommand> Commands { get; set; } = new();

    /// <summary>
    /// Whether this interaction contributed to knowledge base
    /// </summary>
    public bool AddedToKnowledgeBase { get; set; }

    /// <summary>
    /// User feedback rating (1-5 stars, optional)
    /// </summary>
    public int? FeedbackRating { get; set; }

    /// <summary>
    /// User feedback text
    /// </summary>
    public string? FeedbackText { get; set; }

    /// <summary>
    /// Timestamp of the interaction
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// TTL in seconds (30 days = 2,592,000 seconds)
    /// Cosmos DB will auto-delete after this time
    /// </summary>
    [JsonPropertyName("ttl")]
    public int Ttl { get; set; } = 2592000;
}

/// <summary>
/// Represents a command executed during an interaction
/// </summary>
public class ExecutedCommand
{
    /// <summary>
    /// PowerShell or system command executed
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Command output
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Whether command execution was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if command failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Execution timestamp
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
