using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WAiSA.Shared.Models;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// Manages in-memory conversation context for active sessions.
/// Keeps last 10 messages per conversation for AI context.
/// Sessions expire after 1 hour of inactivity.
/// </summary>
public class SessionContextManager
{
    private readonly ConcurrentDictionary<string, SessionContext> _sessions = new();
    private readonly ILogger<SessionContextManager> _logger;
    private readonly Timer _cleanupTimer;
    private const int MaxMessagesInMemory = 20; // Buffer above the 10 we return
    private const int ContextMessageCount = 10; // Number of messages to return for AI
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromHours(1);

    public SessionContextManager(ILogger<SessionContextManager> logger)
    {
        _logger = logger;

        // Run cleanup every 15 minutes
        _cleanupTimer = new Timer(CleanupExpiredSessions, null,
            TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
    }

    /// <summary>
    /// Get the last 10 messages for a conversation (for AI context)
    /// </summary>
    public List<ChatMessage> GetContext(string conversationId)
    {
        if (string.IsNullOrEmpty(conversationId))
        {
            _logger.LogWarning("GetContext called with empty conversationId");
            return new List<ChatMessage>();
        }

        if (_sessions.TryGetValue(conversationId, out var session))
        {
            session.LastAccessed = DateTime.UtcNow;
            var context = session.Messages.TakeLast(ContextMessageCount).ToList();

            _logger.LogInformation(
                "Retrieved {Count} messages for context (conversation: {ConversationId})",
                context.Count, conversationId);

            return context;
        }

        _logger.LogInformation("No existing context for conversation: {ConversationId}", conversationId);
        return new List<ChatMessage>();
    }

    /// <summary>
    /// Add a message to the session context
    /// </summary>
    public void AddMessage(string conversationId, ChatMessage message)
    {
        if (string.IsNullOrEmpty(conversationId))
        {
            _logger.LogWarning("AddMessage called with empty conversationId");
            return;
        }

        var session = _sessions.GetOrAdd(conversationId, _ => new SessionContext
        {
            ConversationId = conversationId,
            CreatedAt = DateTime.UtcNow,
            LastAccessed = DateTime.UtcNow
        });

        session.Messages.Add(message);
        session.LastAccessed = DateTime.UtcNow;

        // Keep only last 20 messages in memory (buffer above the 10 we return)
        if (session.Messages.Count > MaxMessagesInMemory)
        {
            session.Messages.RemoveAt(0);
        }

        _logger.LogDebug(
            "Added message to session context (conversation: {ConversationId}, total: {Count})",
            conversationId, session.Messages.Count);
    }

    /// <summary>
    /// Clear a specific conversation's context
    /// </summary>
    public void ClearContext(string conversationId)
    {
        if (_sessions.TryRemove(conversationId, out _))
        {
            _logger.LogInformation("Cleared context for conversation: {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// Get statistics about active sessions
    /// </summary>
    public SessionStats GetStats()
    {
        return new SessionStats
        {
            ActiveSessions = _sessions.Count,
            TotalMessages = _sessions.Values.Sum(s => s.Messages.Count),
            OldestSession = _sessions.Values.Any()
                ? _sessions.Values.Min(s => s.CreatedAt)
                : (DateTime?)null
        };
    }

    private void CleanupExpiredSessions(object? state)
    {
        try
        {
            var expiredSessions = _sessions
                .Where(kvp => DateTime.UtcNow - kvp.Value.LastAccessed > SessionTimeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var conversationId in expiredSessions)
            {
                if (_sessions.TryRemove(conversationId, out _))
                {
                    _logger.LogInformation(
                        "Removed expired session: {ConversationId}", conversationId);
                }
            }

            if (expiredSessions.Any())
            {
                _logger.LogInformation(
                    "Cleanup complete: removed {Count} expired sessions", expiredSessions.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session cleanup");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}

/// <summary>
/// Represents an active session's context
/// </summary>
internal class SessionContext
{
    public string ConversationId { get; set; } = string.Empty;
    public List<ChatMessage> Messages { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessed { get; set; }
}

/// <summary>
/// Simple chat message for session context
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Statistics about active sessions
/// </summary>
public class SessionStats
{
    public int ActiveSessions { get; set; }
    public int TotalMessages { get; set; }
    public DateTime? OldestSession { get; set; }
}
