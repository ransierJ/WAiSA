using Newtonsoft.Json;

namespace WAiSA.Shared.Models;

/// <summary>
/// Base class for chat history messages stored in Cosmos DB
/// </summary>
public abstract class ChatHistoryMessage
{
    /// <summary>
    /// Unique message ID
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Conversation ID (groups messages together)
    /// </summary>
    [JsonProperty("conversationId")]
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// Message role: "user" or "assistant"
    /// </summary>
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Message content
    /// </summary>
    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When the message was sent
    /// </summary>
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time-to-live in seconds (for Cosmos DB TTL)
    /// 3 years = 94,608,000 seconds
    /// </summary>
    [JsonProperty("ttl")]
    public int? Ttl { get; set; } = 94608000; // 3 years
}

/// <summary>
/// User chat history message (partition key: userId)
/// </summary>
public class UserChatMessage : ChatHistoryMessage
{
    /// <summary>
    /// User/Device ID (partition key)
    /// </summary>
    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Optional: User's display name at time of message
    /// </summary>
    [JsonProperty("userName")]
    public string? UserName { get; set; }

    /// <summary>
    /// Type discriminator for Cosmos DB
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "UserChat";
}

/// <summary>
/// Agent chat history message (partition key: agentId)
/// </summary>
public class AgentChatMessage : ChatHistoryMessage
{
    /// <summary>
    /// Agent ID (partition key)
    /// </summary>
    [JsonProperty("agentId")]
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Agent name at time of message
    /// </summary>
    [JsonProperty("agentName")]
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// User/Device ID that interacted with the agent
    /// </summary>
    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Optional: User's display name
    /// </summary>
    [JsonProperty("userName")]
    public string? UserName { get; set; }

    /// <summary>
    /// Type discriminator for Cosmos DB
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "AgentChat";
}

/// <summary>
/// Search filters for chat history
/// </summary>
public class ChatHistorySearchRequest
{
    /// <summary>
    /// User or Agent ID to search for
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Optional conversation ID to filter by
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// Optional search term (searches in message content)
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Date range filter
    /// </summary>
    public DateRangeFilter DateRange { get; set; } = DateRangeFilter.Last30Days;

    /// <summary>
    /// Custom start date (if DateRange is Custom)
    /// </summary>
    public DateTime? CustomStartDate { get; set; }

    /// <summary>
    /// Custom end date (if DateRange is Custom)
    /// </summary>
    public DateTime? CustomEndDate { get; set; }

    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    public int MaxResults { get; set; } = 100;

    /// <summary>
    /// Get date range for query
    /// </summary>
    public (DateTime StartDate, DateTime EndDate) GetDateRange()
    {
        var now = DateTime.UtcNow;
        return DateRange switch
        {
            DateRangeFilter.Last24Hours => (now.AddDays(-1), now),
            DateRangeFilter.Last7Days => (now.AddDays(-7), now),
            DateRangeFilter.Last30Days => (now.AddDays(-30), now),
            DateRangeFilter.Last90Days => (now.AddDays(-90), now),
            DateRangeFilter.Custom when CustomStartDate.HasValue && CustomEndDate.HasValue =>
                (CustomStartDate.Value, CustomEndDate.Value),
            DateRangeFilter.AllTime => (DateTime.MinValue, now),
            _ => (now.AddDays(-30), now) // Default to last 30 days
        };
    }
}

/// <summary>
/// Date range filter options
/// </summary>
public enum DateRangeFilter
{
    Last24Hours,
    Last7Days,
    Last30Days,
    Last90Days,
    Custom,
    AllTime
}

/// <summary>
/// Search results
/// </summary>
public class ChatHistorySearchResult<T> where T : ChatHistoryMessage
{
    public List<T> Messages { get; set; } = new();
    public int TotalCount { get; set; }
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    public string SearchId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Summary of an agent conversation (for session list table)
/// </summary>
public class AgentConversationSummary
{
    [JsonProperty("conversationId")]
    public string ConversationId { get; set; } = string.Empty;

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("userName")]
    public string? UserName { get; set; }

    [JsonProperty("firstMessage")]
    public string FirstMessage { get; set; } = string.Empty;

    [JsonProperty("lastMessageTime")]
    public DateTime LastMessageTime { get; set; }

    [JsonProperty("messageCount")]
    public int MessageCount { get; set; }
}

/// <summary>
/// Summary of a user conversation (for session list table)
/// </summary>
public class UserConversationSummary
{
    [JsonProperty("conversationId")]
    public string ConversationId { get; set; } = string.Empty;

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("userName")]
    public string? UserName { get; set; }

    [JsonProperty("firstMessage")]
    public string FirstMessage { get; set; } = string.Empty;

    [JsonProperty("lastMessageTime")]
    public DateTime LastMessageTime { get; set; }

    [JsonProperty("messageCount")]
    public int MessageCount { get; set; }
}
