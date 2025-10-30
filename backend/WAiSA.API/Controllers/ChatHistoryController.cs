using Microsoft.AspNetCore.Mvc;
using WAiSA.Infrastructure.Services;
using WAiSA.Shared.Models;

namespace WAiSA.API.Controllers;

/// <summary>
/// Controller for chat history and audit functionality
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatHistoryController : ControllerBase
{
    private readonly ChatHistoryService _chatHistoryService;
    private readonly ILogger<ChatHistoryController> _logger;

    public ChatHistoryController(
        ChatHistoryService chatHistoryService,
        ILogger<ChatHistoryController> logger)
    {
        _chatHistoryService = chatHistoryService;
        _logger = logger;
    }

    #region Agent Chat History

    /// <summary>
    /// Get list of conversations for an agent (for history tab)
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="maxResults">Maximum number of conversations to return (default: 50)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of conversation summaries</returns>
    [HttpGet("agent/{agentId}/conversations")]
    public async Task<ActionResult<List<AgentConversationSummary>>> GetAgentConversations(
        string agentId,
        [FromQuery] int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting conversations for agent: {AgentId} (max: {MaxResults})",
                agentId, maxResults);

            var conversations = await _chatHistoryService.GetAgentConversationsAsync(
                agentId, maxResults, cancellationToken);

            _logger.LogInformation("Retrieved {Count} conversations for agent: {AgentId}",
                conversations.Count, agentId);

            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversations for agent: {AgentId}", agentId);
            return StatusCode(500, new { error = "Failed to retrieve agent conversations" });
        }
    }

    /// <summary>
    /// Get full conversation history for a specific agent conversation
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of messages in conversation (ordered by timestamp)</returns>
    [HttpGet("agent/{agentId}/conversation/{conversationId}")]
    public async Task<ActionResult<List<AgentChatMessage>>> GetAgentConversation(
        string agentId,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting conversation: {ConversationId} for agent: {AgentId}",
                conversationId, agentId);

            var messages = await _chatHistoryService.GetAgentConversationAsync(
                agentId, conversationId, cancellationToken);

            _logger.LogInformation("Retrieved {Count} messages for conversation: {ConversationId}",
                messages.Count, conversationId);

            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation: {ConversationId} for agent: {AgentId}",
                conversationId, agentId);
            return StatusCode(500, new { error = "Failed to retrieve conversation" });
        }
    }

    /// <summary>
    /// Search agent chat history with filters
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="request">Search request with filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results</returns>
    [HttpPost("agent/{agentId}/search")]
    public async Task<ActionResult<ChatHistorySearchResult<AgentChatMessage>>> SearchAgentChat(
        string agentId,
        [FromBody] ChatHistorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Searching agent chat history: {AgentId} (DateRange: {DateRange}, SearchTerm: {SearchTerm}, ConversationId: {ConversationId})",
                agentId, request.DateRange, request.SearchTerm ?? "none", request.ConversationId ?? "none");

            // Set the agent ID in the request
            request.Id = agentId;

            var results = await _chatHistoryService.SearchAgentChatAsync(request, cancellationToken);

            _logger.LogInformation("Agent chat search returned {Count} messages for agent: {AgentId}",
                results.TotalCount, agentId);

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching agent chat history: {AgentId}", agentId);
            return StatusCode(500, new { error = "Failed to search chat history" });
        }
    }

    #endregion

    #region User Chat History

    /// <summary>
    /// Get list of conversations for a user (for history tab)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="maxResults">Maximum number of conversations to return (default: 50)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of conversation summaries</returns>
    [HttpGet("user/{userId}/conversations")]
    public async Task<ActionResult<List<UserConversationSummary>>> GetUserConversations(
        string userId,
        [FromQuery] int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting conversations for user: {UserId} (max: {MaxResults})",
                userId, maxResults);

            var conversations = await _chatHistoryService.GetUserConversationsAsync(
                userId, maxResults, cancellationToken);

            _logger.LogInformation("Retrieved {Count} conversations for user: {UserId}",
                conversations.Count, userId);

            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversations for user: {UserId}", userId);
            return StatusCode(500, new { error = "Failed to retrieve user conversations" });
        }
    }

    /// <summary>
    /// Get full conversation history for a specific user conversation
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of messages in conversation (ordered by timestamp)</returns>
    [HttpGet("user/{userId}/conversation/{conversationId}")]
    public async Task<ActionResult<List<UserChatMessage>>> GetUserConversation(
        string userId,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting conversation: {ConversationId} for user: {UserId}",
                conversationId, userId);

            var messages = await _chatHistoryService.GetUserConversationAsync(
                userId, conversationId, cancellationToken);

            _logger.LogInformation("Retrieved {Count} messages for conversation: {ConversationId}",
                messages.Count, conversationId);

            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation: {ConversationId} for user: {UserId}",
                conversationId, userId);
            return StatusCode(500, new { error = "Failed to retrieve conversation" });
        }
    }

    /// <summary>
    /// Search user chat history with filters
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="request">Search request with filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results</returns>
    [HttpPost("user/{userId}/search")]
    public async Task<ActionResult<ChatHistorySearchResult<UserChatMessage>>> SearchUserChat(
        string userId,
        [FromBody] ChatHistorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Searching user chat history: {UserId} (DateRange: {DateRange}, SearchTerm: {SearchTerm}, ConversationId: {ConversationId})",
                userId, request.DateRange, request.SearchTerm ?? "none", request.ConversationId ?? "none");

            // Set the user ID in the request
            request.Id = userId;

            var results = await _chatHistoryService.SearchUserChatAsync(request, cancellationToken);

            _logger.LogInformation("User chat search returned {Count} messages for user: {UserId}",
                results.TotalCount, userId);

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching user chat history: {UserId}", userId);
            return StatusCode(500, new { error = "Failed to search chat history" });
        }
    }

    #endregion

    #region Health Check

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "ChatHistoryController"
        });
    }

    #endregion
}
