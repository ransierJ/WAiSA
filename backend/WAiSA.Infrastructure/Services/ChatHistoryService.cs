using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WAiSA.Shared.Configuration;
using WAiSA.Shared.Models;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// Service for managing persistent chat history in Cosmos DB.
/// Stores ALL messages for audit and search purposes.
/// Separate from session context (which is in-memory).
/// </summary>
public class ChatHistoryService
{
    private readonly Container _userChatContainer;
    private readonly Container _agentChatContainer;
    private readonly ILogger<ChatHistoryService> _logger;

    public ChatHistoryService(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<ChatHistoryService> logger)
    {
        _logger = logger;
        var cosmosOptions = options.Value;

        var database = cosmosClient.GetDatabase(cosmosOptions.DatabaseName);
        _userChatContainer = database.GetContainer("UserChatHistory");
        _agentChatContainer = database.GetContainer("AgentChatHistory");
    }

    #region User Chat History

    /// <summary>
    /// Save a user chat message to history (fire and forget)
    /// </summary>
    public async Task SaveUserChatAsync(UserChatMessage message)
    {
        try
        {
            await _userChatContainer.CreateItemAsync(
                message,
                new PartitionKey(message.UserId));

            _logger.LogDebug(
                "Saved user chat message: {MessageId} (User: {UserId}, Conversation: {ConversationId})",
                message.Id, message.UserId, message.ConversationId);
        }
        catch (Exception ex)
        {
            // Don't throw - history failure shouldn't block chat
            _logger.LogError(ex,
                "Failed to save user chat message: {MessageId} (User: {UserId})",
                message.Id, message.UserId);
        }
    }

    /// <summary>
    /// Search user chat history
    /// </summary>
    public async Task<ChatHistorySearchResult<UserChatMessage>> SearchUserChatAsync(
        ChatHistorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (startDate, endDate) = request.GetDateRange();
            var userId = request.Id ?? string.Empty;

            // Build query
            var queryText = @"
                SELECT * FROM c
                WHERE c.userId = @userId
                AND c.timestamp >= @startDate
                AND c.timestamp <= @endDate";

            if (!string.IsNullOrEmpty(request.ConversationId))
            {
                queryText += " AND c.conversationId = @conversationId";
            }

            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                queryText += " AND CONTAINS(LOWER(c.content), LOWER(@searchTerm))";
            }

            queryText += " ORDER BY c.timestamp DESC";
            queryText += $" OFFSET 0 LIMIT {request.MaxResults}";

            var queryDef = new QueryDefinition(queryText)
                .WithParameter("@userId", userId)
                .WithParameter("@startDate", startDate)
                .WithParameter("@endDate", endDate);

            if (!string.IsNullOrEmpty(request.ConversationId))
            {
                queryDef = queryDef.WithParameter("@conversationId", request.ConversationId);
            }

            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                queryDef = queryDef.WithParameter("@searchTerm", request.SearchTerm);
            }

            var iterator = _userChatContainer.GetItemQueryIterator<UserChatMessage>(
                queryDef,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

            var messages = new List<UserChatMessage>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                messages.AddRange(response);
            }

            _logger.LogInformation(
                "User chat search returned {Count} messages (User: {UserId})",
                messages.Count, userId);

            return new ChatHistorySearchResult<UserChatMessage>
            {
                Messages = messages,
                TotalCount = messages.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching user chat history: {UserId}", request.Id);
            return new ChatHistorySearchResult<UserChatMessage>();
        }
    }

    /// <summary>
    /// Get messages for a specific conversation
    /// </summary>
    public async Task<List<UserChatMessage>> GetUserConversationAsync(
        string userId,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryDef = new QueryDefinition(
                "SELECT * FROM c WHERE c.userId = @userId AND c.conversationId = @conversationId ORDER BY c.timestamp ASC")
                .WithParameter("@userId", userId)
                .WithParameter("@conversationId", conversationId);

            var iterator = _userChatContainer.GetItemQueryIterator<UserChatMessage>(
                queryDef,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

            var messages = new List<UserChatMessage>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                messages.AddRange(response);
            }

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting conversation: {ConversationId} for user: {UserId}",
                conversationId, userId);
            return new List<UserChatMessage>();
        }
    }

    /// <summary>
    /// Get list of conversations for a user (for history tab)
    /// </summary>
    public async Task<List<UserConversationSummary>> GetUserConversationsAsync(
        string userId,
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Get all messages ordered by timestamp DESC
            var queryDef = new QueryDefinition(@"
                SELECT * FROM c
                WHERE c.userId = @userId
                ORDER BY c.timestamp DESC")
                .WithParameter("@userId", userId);

            var iterator = _userChatContainer.GetItemQueryIterator<UserChatMessage>(
                queryDef,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

            var allMessages = new List<UserChatMessage>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                allMessages.AddRange(response);
            }

            // Step 2: Group by conversationId and build summaries
            var conversations = allMessages
                .GroupBy(m => m.ConversationId)
                .Select(g =>
                {
                    var messages = g.OrderBy(m => m.Timestamp).ToList();
                    var firstUserMessage = messages.FirstOrDefault(m => m.Role == "user");

                    return new UserConversationSummary
                    {
                        ConversationId = g.Key,
                        UserId = userId,
                        UserName = messages.FirstOrDefault()?.UserName,
                        FirstMessage = firstUserMessage?.Content ?? "No user message",
                        LastMessageTime = g.Max(m => m.Timestamp),
                        MessageCount = g.Count()
                    };
                })
                .OrderByDescending(c => c.LastMessageTime)
                .Take(maxResults)
                .ToList();

            _logger.LogInformation(
                "Found {Count} conversations for user: {UserId}",
                conversations.Count, userId);

            return conversations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user conversations: {UserId}", userId);
            return new List<UserConversationSummary>();
        }
    }

    #endregion

    #region Agent Chat History

    /// <summary>
    /// Save an agent chat message to history (fire and forget)
    /// </summary>
    public async Task SaveAgentChatAsync(AgentChatMessage message)
    {
        try
        {
            await _agentChatContainer.CreateItemAsync(
                message,
                new PartitionKey(message.AgentId));

            _logger.LogDebug(
                "Saved agent chat message: {MessageId} (Agent: {AgentId}, Conversation: {ConversationId})",
                message.Id, message.AgentId, message.ConversationId);
        }
        catch (Exception ex)
        {
            // Don't throw - history failure shouldn't block chat
            _logger.LogError(ex,
                "Failed to save agent chat message: {MessageId} (Agent: {AgentId})",
                message.Id, message.AgentId);
        }
    }

    /// <summary>
    /// Search agent chat history
    /// </summary>
    public async Task<ChatHistorySearchResult<AgentChatMessage>> SearchAgentChatAsync(
        ChatHistorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (startDate, endDate) = request.GetDateRange();
            var agentId = request.Id ?? string.Empty;

            // Build query
            var queryText = @"
                SELECT * FROM c
                WHERE c.agentId = @agentId
                AND c.timestamp >= @startDate
                AND c.timestamp <= @endDate";

            if (!string.IsNullOrEmpty(request.ConversationId))
            {
                queryText += " AND c.conversationId = @conversationId";
            }

            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                queryText += " AND CONTAINS(LOWER(c.content), LOWER(@searchTerm))";
            }

            queryText += " ORDER BY c.timestamp DESC";
            queryText += $" OFFSET 0 LIMIT {request.MaxResults}";

            var queryDef = new QueryDefinition(queryText)
                .WithParameter("@agentId", agentId)
                .WithParameter("@startDate", startDate)
                .WithParameter("@endDate", endDate);

            if (!string.IsNullOrEmpty(request.ConversationId))
            {
                queryDef = queryDef.WithParameter("@conversationId", request.ConversationId);
            }

            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                queryDef = queryDef.WithParameter("@searchTerm", request.SearchTerm);
            }

            var iterator = _agentChatContainer.GetItemQueryIterator<AgentChatMessage>(
                queryDef,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(agentId) });

            var messages = new List<AgentChatMessage>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                messages.AddRange(response);
            }

            _logger.LogInformation(
                "Agent chat search returned {Count} messages (Agent: {AgentId})",
                messages.Count, agentId);

            return new ChatHistorySearchResult<AgentChatMessage>
            {
                Messages = messages,
                TotalCount = messages.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching agent chat history: {AgentId}", request.Id);
            return new ChatHistorySearchResult<AgentChatMessage>();
        }
    }

    /// <summary>
    /// Get messages for a specific agent conversation
    /// </summary>
    public async Task<List<AgentChatMessage>> GetAgentConversationAsync(
        string agentId,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryDef = new QueryDefinition(
                "SELECT * FROM c WHERE c.agentId = @agentId AND c.conversationId = @conversationId ORDER BY c.timestamp ASC")
                .WithParameter("@agentId", agentId)
                .WithParameter("@conversationId", conversationId);

            var iterator = _agentChatContainer.GetItemQueryIterator<AgentChatMessage>(
                queryDef,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(agentId) });

            var messages = new List<AgentChatMessage>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                messages.AddRange(response);
            }

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting agent conversation: {ConversationId} for agent: {AgentId}",
                conversationId, agentId);
            return new List<AgentChatMessage>();
        }
    }

    /// <summary>
    /// Get list of conversations for an agent (for history tab)
    /// </summary>
    public async Task<List<AgentConversationSummary>> GetAgentConversationsAsync(
        string agentId,
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Get all messages ordered by timestamp DESC
            var queryDef = new QueryDefinition(@"
                SELECT * FROM c
                WHERE c.agentId = @agentId
                ORDER BY c.timestamp DESC")
                .WithParameter("@agentId", agentId);

            var iterator = _agentChatContainer.GetItemQueryIterator<AgentChatMessage>(
                queryDef,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(agentId) });

            var allMessages = new List<AgentChatMessage>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                allMessages.AddRange(response);
            }

            // Step 2: Group by conversationId and build summaries
            var conversations = allMessages
                .GroupBy(m => m.ConversationId)
                .Select(g =>
                {
                    var messages = g.OrderBy(m => m.Timestamp).ToList();
                    var firstUserMessage = messages.FirstOrDefault(m => m.Role == "user");

                    return new AgentConversationSummary
                    {
                        ConversationId = g.Key,
                        UserId = messages.FirstOrDefault()?.UserId ?? "Unknown",
                        UserName = messages.FirstOrDefault()?.UserName,
                        FirstMessage = firstUserMessage?.Content ?? "No user message",
                        LastMessageTime = g.Max(m => m.Timestamp),
                        MessageCount = g.Count()
                    };
                })
                .OrderByDescending(c => c.LastMessageTime)
                .Take(maxResults)
                .ToList();

            _logger.LogInformation(
                "Found {Count} conversations for agent: {AgentId}",
                conversations.Count, agentId);

            return conversations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent conversations: {AgentId}", agentId);
            return new List<AgentConversationSummary>();
        }
    }

    #endregion
}
