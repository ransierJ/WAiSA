using WAiSA.Shared.Models;

namespace WAiSA.Core.Interfaces;

/// <summary>
/// Service for managing agent-specific chat history with audit logging
/// </summary>
public interface IAgentChatHistoryService
{
    /// <summary>
    /// Save a chat message to agent history
    /// </summary>
    /// <param name="chatHistory">Chat history entry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveChatMessageAsync(
        AgentChatHistory chatHistory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent chat messages for an agent (for context)
    /// </summary>
    /// <param name="agentId">Agent identifier</param>
    /// <param name="count">Number of messages to retrieve (default 10)</param>
    /// <param name="sessionId">Optional session ID to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of recent chat messages</returns>
    Task<List<AgentChatHistory>> GetRecentChatMessagesAsync(
        string agentId,
        int count = 10,
        string? sessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get chat history for an agent with pagination
    /// </summary>
    /// <param name="agentId">Agent identifier</param>
    /// <param name="skip">Number of messages to skip</param>
    /// <param name="take">Number of messages to take</param>
    /// <param name="sessionId">Optional session ID to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of chat messages</returns>
    Task<List<AgentChatHistory>> GetChatHistoryAsync(
        string agentId,
        int skip = 0,
        int take = 50,
        string? sessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all sessions for an agent
    /// </summary>
    /// <param name="agentId">Agent identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of unique session IDs</returns>
    Task<List<string>> GetAgentSessionsAsync(
        string agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search chat history by content
    /// </summary>
    /// <param name="agentId">Agent identifier</param>
    /// <param name="searchQuery">Search query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching chat messages</returns>
    Task<List<AgentChatHistory>> SearchChatHistoryAsync(
        string agentId,
        string searchQuery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete chat history for a session (for GDPR compliance)
    /// </summary>
    /// <param name="agentId">Agent identifier</param>
    /// <param name="sessionId">Session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteSessionAsync(
        string agentId,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get chat statistics for an agent
    /// </summary>
    /// <param name="agentId">Agent identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Statistics about agent chat usage</returns>
    Task<AgentChatStatistics> GetChatStatisticsAsync(
        string agentId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about agent chat usage
/// </summary>
public class AgentChatStatistics
{
    public int TotalMessages { get; set; }
    public int TotalSessions { get; set; }
    public int TotalCommands { get; set; }
    public int SuccessfulCommands { get; set; }
    public int FailedCommands { get; set; }
    public DateTime? FirstMessageAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int TotalTokensUsed { get; set; }
}
