using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WAiSA.Core.Interfaces;
using WAiSA.Shared.Configuration;
using WAiSA.Shared.Models;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// Agent Chat History Service implementation using Azure Cosmos DB
/// Provides permanent audit trail for agent-specific conversations
/// </summary>
public class AgentChatHistoryService : IAgentChatHistoryService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _chatHistoryContainer;
    private readonly CosmosDbOptions _options;
    private readonly ILogger<AgentChatHistoryService> _logger;

    public AgentChatHistoryService(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<AgentChatHistoryService> logger)
    {
        _cosmosClient = cosmosClient;
        _options = options.Value;
        _logger = logger;

        // Get database and container from injected Cosmos client
        var database = _cosmosClient.GetDatabase(_options.DatabaseName);
        _chatHistoryContainer = database.GetContainer(_options.AgentChatHistoryContainer);
    }

    /// <summary>
    /// Save a chat message to agent history
    /// </summary>
    public async Task SaveChatMessageAsync(
        AgentChatHistory chatHistory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _chatHistoryContainer.CreateItemAsync(
                chatHistory,
                new PartitionKey(chatHistory.AgentId),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Saved chat message for agent {AgentId}, session {SessionId}, role {Role}",
                chatHistory.AgentId,
                chatHistory.SessionId,
                chatHistory.Role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error saving chat message for agent {AgentId}, session {SessionId}",
                chatHistory.AgentId,
                chatHistory.SessionId);
            throw;
        }
    }

    /// <summary>
    /// Get recent chat messages for an agent (for context)
    /// </summary>
    public async Task<List<AgentChatHistory>> GetRecentChatMessagesAsync(
        string agentId,
        int count = 10,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryBuilder = _chatHistoryContainer
                .GetItemLinqQueryable<AgentChatHistory>()
                .Where(c => c.AgentId == agentId);

            if (!string.IsNullOrEmpty(sessionId))
            {
                queryBuilder = queryBuilder.Where(c => c.SessionId == sessionId);
            }

            var query = queryBuilder
                .OrderByDescending(c => c.Timestamp)
                .Take(count)
                .ToFeedIterator();

            var messages = new List<AgentChatHistory>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                messages.AddRange(response);
            }

            // Return in chronological order (oldest first)
            return messages.OrderBy(m => m.Timestamp).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving recent chat messages for agent {AgentId}",
                agentId);
            return new List<AgentChatHistory>();
        }
    }

    /// <summary>
    /// Get chat history for an agent with pagination
    /// </summary>
    public async Task<List<AgentChatHistory>> GetChatHistoryAsync(
        string agentId,
        int skip = 0,
        int take = 50,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryBuilder = _chatHistoryContainer
                .GetItemLinqQueryable<AgentChatHistory>()
                .Where(c => c.AgentId == agentId);

            if (!string.IsNullOrEmpty(sessionId))
            {
                queryBuilder = queryBuilder.Where(c => c.SessionId == sessionId);
            }

            var query = queryBuilder
                .OrderByDescending(c => c.Timestamp)
                .Skip(skip)
                .Take(take)
                .ToFeedIterator();

            var messages = new List<AgentChatHistory>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                messages.AddRange(response);
            }

            return messages.OrderBy(m => m.Timestamp).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving chat history for agent {AgentId}",
                agentId);
            return new List<AgentChatHistory>();
        }
    }

    /// <summary>
    /// Get all sessions for an agent
    /// </summary>
    public async Task<List<string>> GetAgentSessionsAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _chatHistoryContainer
                .GetItemLinqQueryable<AgentChatHistory>()
                .Where(c => c.AgentId == agentId)
                .Select(c => c.SessionId)
                .ToFeedIterator();

            var sessions = new HashSet<string>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                foreach (var sessionId in response)
                {
                    sessions.Add(sessionId);
                }
            }

            return sessions.OrderByDescending(s => s).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving sessions for agent {AgentId}",
                agentId);
            return new List<string>();
        }
    }

    /// <summary>
    /// Search chat history by content
    /// </summary>
    public async Task<List<AgentChatHistory>> SearchChatHistoryAsync(
        string agentId,
        string searchQuery,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _chatHistoryContainer
                .GetItemLinqQueryable<AgentChatHistory>()
                .Where(c => c.AgentId == agentId &&
                           c.Content.Contains(searchQuery))
                .OrderByDescending(c => c.Timestamp)
                .ToFeedIterator();

            var messages = new List<AgentChatHistory>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                messages.AddRange(response);
            }

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error searching chat history for agent {AgentId}",
                agentId);
            return new List<AgentChatHistory>();
        }
    }

    /// <summary>
    /// Delete chat history for a session (for GDPR compliance)
    /// </summary>
    public async Task DeleteSessionAsync(
        string agentId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _chatHistoryContainer
                .GetItemLinqQueryable<AgentChatHistory>()
                .Where(c => c.AgentId == agentId && c.SessionId == sessionId)
                .ToFeedIterator();

            var deleteCount = 0;
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                foreach (var message in response)
                {
                    await _chatHistoryContainer.DeleteItemAsync<AgentChatHistory>(
                        message.Id,
                        new PartitionKey(agentId),
                        cancellationToken: cancellationToken);
                    deleteCount++;
                }
            }

            _logger.LogInformation(
                "Deleted {Count} messages for agent {AgentId}, session {SessionId}",
                deleteCount,
                agentId,
                sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error deleting session for agent {AgentId}, session {SessionId}",
                agentId,
                sessionId);
            throw;
        }
    }

    /// <summary>
    /// Get chat statistics for an agent
    /// </summary>
    public async Task<AgentChatStatistics> GetChatStatisticsAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _chatHistoryContainer
                .GetItemLinqQueryable<AgentChatHistory>()
                .Where(c => c.AgentId == agentId)
                .ToFeedIterator();

            var stats = new AgentChatStatistics();
            var sessions = new HashSet<string>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                foreach (var message in response)
                {
                    stats.TotalMessages++;
                    sessions.Add(message.SessionId);

                    if (message.Commands != null)
                    {
                        stats.TotalCommands += message.Commands.Count;
                        stats.SuccessfulCommands += message.Commands.Count(c => c.Success);
                        stats.FailedCommands += message.Commands.Count(c => !c.Success);
                    }

                    if (message.TokensUsed.HasValue)
                    {
                        stats.TotalTokensUsed += message.TokensUsed.Value;
                    }

                    if (!stats.FirstMessageAt.HasValue || message.Timestamp < stats.FirstMessageAt)
                    {
                        stats.FirstMessageAt = message.Timestamp;
                    }

                    if (!stats.LastMessageAt.HasValue || message.Timestamp > stats.LastMessageAt)
                    {
                        stats.LastMessageAt = message.Timestamp;
                    }
                }
            }

            stats.TotalSessions = sessions.Count;

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving chat statistics for agent {AgentId}",
                agentId);
            return new AgentChatStatistics();
        }
    }
}
