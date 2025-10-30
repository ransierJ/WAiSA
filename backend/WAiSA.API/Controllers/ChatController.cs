using Microsoft.AspNetCore.Mvc;
using WAiSA.Core.Interfaces;
using WAiSA.Shared.Models;
using WAiSA.API.Models;
using WAiSA.Infrastructure.Services; // NEW: For SessionContextManager and ChatHistoryService
using System.Linq;

namespace WAiSA.API.Controllers;

/// <summary>
/// Chat API controller for AI-powered Windows system administration
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IAIOrchestrationService _aiService;
    private readonly IDeviceMemoryService _deviceMemoryService;
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly ICascadingSearchService _cascadingSearchService;
    private readonly ICascadeOrchestrator _cascadeOrchestrator;
    private readonly IAgentService _agentService;
    private readonly ICommandClassificationService _commandClassificationService;
    private readonly IAgentChatHistoryService _agentChatHistoryService;
    private readonly SessionContextManager _sessionContext; // NEW
    private readonly ChatHistoryService _chatHistory; // NEW
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IAIOrchestrationService aiService,
        IDeviceMemoryService deviceMemoryService,
        IKnowledgeBaseService knowledgeBaseService,
        ICascadingSearchService cascadingSearchService,
        ICascadeOrchestrator cascadeOrchestrator,
        IAgentService agentService,
        ICommandClassificationService commandClassificationService,
        IAgentChatHistoryService agentChatHistoryService,
        SessionContextManager sessionContext, // NEW
        ChatHistoryService chatHistory, // NEW
        ILogger<ChatController> logger)
    {
        _aiService = aiService;
        _deviceMemoryService = deviceMemoryService;
        _knowledgeBaseService = knowledgeBaseService;
        _cascadingSearchService = cascadingSearchService;
        _cascadeOrchestrator = cascadeOrchestrator;
        _agentService = agentService;
        _commandClassificationService = commandClassificationService;
        _agentChatHistoryService = agentChatHistoryService;
        _sessionContext = sessionContext; // NEW
        _chatHistory = chatHistory; // NEW
        _logger = logger;
    }

    /// <summary>
    /// Send a chat message and get AI response
    /// </summary>
    /// <param name="request">Chat request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI response with executed commands</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponseDto>> SendMessage(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing chat request for device {DeviceId}: {Message}",
            request?.DeviceId ?? "null", request?.Message ?? "null");

        // DEBUG: Log the incoming conversationId to diagnose memory bug
        _logger.LogWarning("CONVERSATIONID_DEBUG: Received conversationId = '{ConversationId}' (IsNull: {IsNull}, IsEmpty: {IsEmpty})",
            request?.ConversationId ?? "NULL",
            request?.ConversationId == null,
            string.IsNullOrEmpty(request?.ConversationId));

        try
        {
            if (request == null)
                return BadRequest("Request body is required");

            if (string.IsNullOrWhiteSpace(request.DeviceId))
                return BadRequest("Device ID is required");

            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Message is required");

            _logger.LogInformation(
                "Processing chat message from device {DeviceId}",
                request.DeviceId);

            // Get device context
            var deviceMemory = await _deviceMemoryService.GetDeviceMemoryAsync(
                request.DeviceId,
                cancellationToken);

            // Get agent information if deviceId is an agentId
            Core.Entities.Agent? agent = null;
            if (Guid.TryParse(request.DeviceId, out var agentIdGuid))
            {
                agent = await _agentService.GetAgentAsync(agentIdGuid);
                _logger.LogInformation("AGENT_CONTEXT_DEBUG: Agent lookup for {DeviceId}: {Found}",
                    request.DeviceId, agent != null ? "FOUND" : "NOT FOUND");
            }

            // NEW: Get conversation context from in-memory session manager (FAST!)
            // Use conversationId if provided, otherwise use deviceId (for agent chats)
            var conversationId = !string.IsNullOrEmpty(request.ConversationId)
                ? request.ConversationId
                : request.DeviceId;

            _logger.LogInformation("CONTEXT_DEBUG: Retrieving session context for conversation: {ConversationId} (using {Source})",
                conversationId, string.IsNullOrEmpty(request.ConversationId) ? "deviceId" : "conversationId");

            var sessionMessages = _sessionContext.GetContext(conversationId);

            _logger.LogInformation("CONTEXT_DEBUG: Retrieved {Count} messages from session context for: {ConversationId}",
                sessionMessages.Count, conversationId);

            if (sessionMessages.Any())
            {
                foreach (var msg in sessionMessages)
                {
                    _logger.LogInformation("CONTEXT_DEBUG: SessionMessage - Role: {Role}, Content: '{Content}'",
                        msg.Role, msg.Content?.Take(100));
                }
            }
            else
            {
                _logger.LogWarning("CONTEXT_DEBUG: No messages found in session context for: {ConversationId}", conversationId);
            }

            // Convert ChatMessage format to Interaction format for cascade orchestrator
            var recentInteractions = ConvertChatMessagesToInteractions(sessionMessages);

            _logger.LogInformation("CONTEXT_DEBUG: Converted {SessionCount} messages to {InteractionCount} interactions",
                sessionMessages.Count, recentInteractions.Count);

            if (recentInteractions.Any())
            {
                foreach (var interaction in recentInteractions.Take(3))
                {
                    _logger.LogInformation("CONTEXT_DEBUG: Interaction - User: '{User}', Assistant: '{Assistant}'",
                        interaction.UserMessage?.Take(50), interaction.AssistantResponse?.Take(50));
                }
            }
            else
            {
                _logger.LogWarning("CONTEXT_DEBUG: No interactions available for AI context!");
            }

            // BYPASS CASCADE - Call AI service directly to avoid double-searching
            // The LLM has intelligent MCP search functions built-in (search_microsoft_docs, search_web)
            // The cascade was causing:
            //   1. KB stage searches and returns raw docs
            //   2. LLM stage searches AGAIN via MCP functions
            //   3. Result: 3+ minute delays and documentation dumps in responses

            _logger.LogInformation("[ChatController] Calling AI service directly for query: {Query}", request.Message);

            var aiResponse = await _aiService.ProcessMessageAsync(
                request.DeviceId,
                request.Message,
                recentInteractions,
                cancellationToken);

            // Extract knowledge references for response metadata (empty for now since we bypassed cascade)
            var relevantKnowledge = new List<KnowledgeSearchResult>();

            if (!aiResponse.Success)
            {
                return StatusCode(500, new ChatResponseDto
                {
                    Message = aiResponse.ErrorMessage ?? "An error occurred processing your request",
                    Success = false
                });
            }

            // Extract commands from AI response
            var commands = _commandClassificationService.ExtractCommandsFromResponse(aiResponse.Message);

            var queuedCommands = new List<ExecutedCommandDto>();
            var pendingApprovals = new List<Guid>();

            // Process and queue commands if agent is available
            if (agent != null && commands.Any())
            {
                _logger.LogInformation("Extracted {Count} commands from AI response for agent {AgentId}",
                    commands.Count, agent.AgentId);

                foreach (var command in commands)
                {
                    var classification = _commandClassificationService.ClassifyCommand(command);

                    _logger.LogInformation(
                        "Command classified: {Command} - Risk: {Risk}, RequiresApproval: {Approval}",
                        command, classification.RiskLevel, classification.RequiresApproval);

                    // Queue command
                    var commandId = await _agentService.QueueCommandAsync(
                        agent.AgentId,
                        command,
                        $"AI Chat: {request.Message.Substring(0, Math.Min(50, request.Message.Length))}...",
                        classification.SuggestedTimeoutSeconds,
                        classification.RequiresApproval,
                        "AI Assistant",
                        request.DeviceId);

                    if (classification.RequiresApproval)
                    {
                        pendingApprovals.Add(commandId);
                        queuedCommands.Add(new ExecutedCommandDto
                        {
                            Command = command,
                            Output = $"⏳ Pending approval (Risk: {classification.RiskLevel}) - {classification.Reasoning}",
                            Success = false,
                            ExecutedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        // Wait for command execution (with 60 second timeout)
                        _logger.LogInformation("Waiting for command {CommandId} to execute...", commandId);

                        var completedCommand = await _agentService.WaitForCommandCompletionAsync(
                            commandId,
                            timeoutSeconds: 60,
                            cancellationToken);

                        if (completedCommand != null && completedCommand.Status == Core.Entities.CommandStatus.Completed)
                        {
                            queuedCommands.Add(new ExecutedCommandDto
                            {
                                Command = command,
                                Output = completedCommand.Output ?? "Command executed successfully",
                                Success = true,
                                ExecutedAt = completedCommand.CompletedAt ?? DateTime.UtcNow
                            });
                        }
                        else if (completedCommand != null && completedCommand.Status == Core.Entities.CommandStatus.Failed)
                        {
                            queuedCommands.Add(new ExecutedCommandDto
                            {
                                Command = command,
                                Output = completedCommand.Output ?? "Command failed",
                                ErrorMessage = completedCommand.Error,
                                Success = false,
                                ExecutedAt = completedCommand.CompletedAt ?? DateTime.UtcNow
                            });
                        }
                        else
                        {
                            // Timeout or still executing
                            queuedCommands.Add(new ExecutedCommandDto
                            {
                                Command = command,
                                Output = $"⏳ Command queued but execution pending (timeout after 60s). Check command history for results.",
                                Success = false,
                                ExecutedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
            }

            // Two-pass approach: Make second AI call to interpret results conversationally
            var responseMessage = aiResponse.Message;
            const int maxRetries = 3;
            var retryCount = 0;

            if (queuedCommands.Any() && !pendingApprovals.Any())
            {
                // Retry loop: Keep trying until success or max retries
                while (retryCount < maxRetries && queuedCommands.Any(c => !c.Success))
                {
                    // Build context with conversation history and command results for AI interpretation
                    var resultsContext = new System.Text.StringBuilder();

                    // Add recent conversation history so AI remembers context
                    if (recentInteractions.Any())
                    {
                        resultsContext.AppendLine("**Recent Conversation History:**");
                        foreach (var pastInteraction in recentInteractions.OrderBy(i => i.Timestamp))
                        {
                            resultsContext.AppendLine($"\nUser: {pastInteraction.UserMessage}");
                            resultsContext.AppendLine($"Assistant: {pastInteraction.AssistantResponse}");
                        }
                        resultsContext.AppendLine();
                    }

                    resultsContext.AppendLine("**Current Command Results:**");
                    resultsContext.AppendLine("The following commands were just executed on the system:");
                    resultsContext.AppendLine();

                    foreach (var cmd in queuedCommands.Where(c => c.Success))
                    {
                        resultsContext.AppendLine($"Command: {cmd.Command}");
                        resultsContext.AppendLine($"Output: {cmd.Output}");
                        resultsContext.AppendLine();
                    }

                    foreach (var cmd in queuedCommands.Where(c => !c.Success))
                    {
                        resultsContext.AppendLine($"Command: {cmd.Command}");
                        resultsContext.AppendLine($"Error: {cmd.ErrorMessage ?? cmd.Output}");
                        resultsContext.AppendLine();
                    }

                    resultsContext.AppendLine($"User's current question: {request.Message}");
                    resultsContext.AppendLine();

                    if (retryCount == 0)
                    {
                        resultsContext.AppendLine("Please interpret these command results conversationally in the context of the conversation history. Respond to the user in a friendly, natural way. Keep it short (2-3 sentences) and be proactive about offering help. If the user is asking a follow-up question, make sure to connect it to what was previously discussed.");
                    }
                    else
                    {
                        resultsContext.AppendLine($"Retry attempt {retryCount + 1}/{maxRetries}. Some commands failed. If fixable, provide corrected commands in code blocks to retry automatically. Otherwise, explain the issue conversationally.");
                    }

                    // Make AI call to get conversational interpretation or retry command
                    _logger.LogInformation("Making AI call to interpret command results (attempt {RetryCount})", retryCount + 1);

                    var interpretationResponse = await _aiService.ProcessMessageAsync(
                        request.DeviceId,
                        resultsContext.ToString(),
                        recentInteractions,
                        cancellationToken);

                    if (interpretationResponse.Success)
                    {
                        responseMessage = interpretationResponse.Message;

                        // Check if AI generated retry commands
                        var retryCommands = _commandClassificationService.ExtractCommandsFromResponse(interpretationResponse.Message);

                        if (retryCommands.Any() && agent != null)
                        {
                            _logger.LogInformation("AI generated {Count} retry commands, executing them...", retryCommands.Count);

                            // Clear previous failed commands and execute retry commands
                            queuedCommands.Clear();

                            foreach (var retryCommand in retryCommands)
                            {
                                var classification = _commandClassificationService.ClassifyCommand(retryCommand);

                                var commandId = await _agentService.QueueCommandAsync(
                                    agent.AgentId,
                                    retryCommand,
                                    $"AI Retry: {request.Message.Substring(0, Math.Min(50, request.Message.Length))}...",
                                    classification.SuggestedTimeoutSeconds,
                                    false, // Don't require approval for retry commands
                                    "AI Assistant (Retry)",
                                    request.DeviceId);

                                // Wait for command execution
                                var completedCommand = await _agentService.WaitForCommandCompletionAsync(
                                    commandId,
                                    timeoutSeconds: 60,
                                    cancellationToken);

                                if (completedCommand != null && completedCommand.Status == Core.Entities.CommandStatus.Completed)
                                {
                                    queuedCommands.Add(new ExecutedCommandDto
                                    {
                                        Command = retryCommand,
                                        Output = completedCommand.Output ?? "Command executed successfully",
                                        Success = true,
                                        ExecutedAt = completedCommand.CompletedAt ?? DateTime.UtcNow
                                    });
                                }
                                else if (completedCommand != null && completedCommand.Status == Core.Entities.CommandStatus.Failed)
                                {
                                    queuedCommands.Add(new ExecutedCommandDto
                                    {
                                        Command = retryCommand,
                                        Output = completedCommand.Output ?? "Command failed",
                                        ErrorMessage = completedCommand.Error,
                                        Success = false,
                                        ExecutedAt = completedCommand.CompletedAt ?? DateTime.UtcNow
                                    });
                                }
                                else
                                {
                                    queuedCommands.Add(new ExecutedCommandDto
                                    {
                                        Command = retryCommand,
                                        Output = "Command execution timeout",
                                        Success = false,
                                        ExecutedAt = DateTime.UtcNow
                                    });
                                }
                            }

                            retryCount++;
                        }
                        else
                        {
                            // No retry commands found, break out of retry loop
                            _logger.LogInformation("No retry commands found or all commands succeeded, exiting retry loop");
                            break;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to get AI interpretation on retry attempt {RetryCount}", retryCount + 1);
                        break;
                    }
                }
            }
            else if (pendingApprovals.Any())
            {
                // For pending approvals, use a simple conversational message
                var commandList = string.Join(", ", queuedCommands.Select(c => $"`{c.Command}`"));
                responseMessage = $"I'd like to run these commands for you: {commandList}. However, they require approval before I can execute them. Would you like to approve them?";
            }

            // NEW ARCHITECTURE: Dual-storage approach
            // 1. Session Context (in-memory, fast) - for AI context
            // 2. Chat History (Cosmos DB, persistent) - for audit and search

            var timestamp = DateTime.UtcNow;

            // Step 1: Add messages to session context (SYNC, FAST)
            _logger.LogInformation("CONTEXT_DEBUG: Adding messages to session context for conversation: {ConversationId}",
                conversationId);
            _logger.LogInformation("CONTEXT_DEBUG: User message: '{UserMsg}'", request.Message.Take(100));
            _logger.LogInformation("CONTEXT_DEBUG: Assistant response: '{AssistantMsg}'", responseMessage.Take(100));

            _sessionContext.AddMessage(conversationId, new ChatMessage
            {
                Role = "user",
                Content = request.Message,
                Timestamp = timestamp
            });

            _sessionContext.AddMessage(conversationId, new ChatMessage
            {
                Role = "assistant",
                Content = responseMessage,
                Timestamp = timestamp
            });

            _logger.LogInformation("CONTEXT_DEBUG: Successfully added messages to session context for: {ConversationId}", conversationId);

            // Step 2: Save to persistent history (ASYNC, FIRE-AND-FORGET)
            // This runs in background and doesn't block the response
            _ = Task.Run(async () =>
            {
                try
                {
                    if (agent != null)
                    {
                        // Agent chat: Save to AgentChatHistory
                        var agentIdString = agent.AgentId.ToString().ToLowerInvariant();

                        _logger.LogInformation("Saving agent chat history for agent {AgentId}", agentIdString);

                        // Save user message
                        var userMessage = new AgentChatMessage
                        {
                            AgentId = agentIdString,
                            UserId = request.DeviceId,
                            ConversationId = conversationId,
                            Role = "user",
                            Content = request.Message,
                            Timestamp = timestamp,
                            AgentName = agent.ComputerName ?? "Unknown"
                        };
                        await _chatHistory.SaveAgentChatAsync(userMessage);

                        // Save assistant message
                        var assistantMessage = new AgentChatMessage
                        {
                            AgentId = agentIdString,
                            UserId = request.DeviceId,
                            ConversationId = conversationId,
                            Role = "assistant",
                            Content = responseMessage,
                            Timestamp = timestamp,
                            AgentName = agent.ComputerName ?? "Unknown"
                        };
                        await _chatHistory.SaveAgentChatAsync(assistantMessage);

                        _logger.LogInformation("Saved agent chat history successfully");
                    }
                    else
                    {
                        // User chat: Save to UserChatHistory
                        _logger.LogInformation("Saving user chat history for user {UserId}", request.DeviceId);

                        // Save user message
                        var userMessage = new UserChatMessage
                        {
                            UserId = request.DeviceId,
                            ConversationId = conversationId,
                            Role = "user",
                            Content = request.Message,
                            Timestamp = timestamp
                        };
                        await _chatHistory.SaveUserChatAsync(userMessage);

                        // Save assistant message
                        var assistantMessage = new UserChatMessage
                        {
                            UserId = request.DeviceId,
                            ConversationId = conversationId,
                            Role = "assistant",
                            Content = responseMessage,
                            Timestamp = timestamp
                        };
                        await _chatHistory.SaveUserChatAsync(assistantMessage);

                        _logger.LogInformation("Saved user chat history successfully");
                    }
                }
                catch (Exception historyEx)
                {
                    // Log but don't throw - history failure shouldn't affect chat
                    _logger.LogError(historyEx,
                        "Failed to save chat history for conversation {ConversationId}",
                        conversationId);
                }
            });

            return Ok(new ChatResponseDto
            {
                Message = responseMessage,
                ExecutedCommands = queuedCommands,
                ActivityLogs = aiResponse.ActivityLogs.Select(a => new ActivityLogDto
                {
                    Timestamp = a.Timestamp,
                    Type = a.Type,
                    Message = a.Message,
                    Icon = a.Icon,
                    Details = a.Details
                }).ToList(),
                Success = true,
                TokensUsed = aiResponse.TokensUsed,
                ContextSummary = deviceMemory?.ContextSummary,
                RelevantKnowledge = relevantKnowledge.Select(k => new KnowledgeReferenceDto
                {
                    Id = k.Entry.Id,
                    Title = k.Entry.Title,
                    SimilarityScore = k.SimilarityScore
                }).ToList(),
                CascadeMetadata = new Dictionary<string, object>
                {
                    { "Mode", "Direct AI Service" },
                    { "Note", "Cascade bypassed to prevent double-searching" }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message: {Message}. Stack trace: {StackTrace}",
                ex.Message, ex.StackTrace);

            if (ex.InnerException != null)
            {
                _logger.LogError("Inner exception: {InnerMessage}. Inner stack trace: {InnerStackTrace}",
                    ex.InnerException.Message, ex.InnerException.StackTrace);
            }

            return StatusCode(500, new ChatResponseDto
            {
                Message = "An unexpected error occurred. Please try again.",
                Success = false
            });
        }
    }

    /// <summary>
    /// Get chat history for a device
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <param name="count">Number of recent interactions to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of interactions</returns>
    [HttpGet("history/{deviceId}")]
    [ProducesResponseType(typeof(List<InteractionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<InteractionDto>>> GetHistory(
        string deviceId,
        [FromQuery] int count = 20,
        CancellationToken cancellationToken = default)
    {
        var interactions = await _deviceMemoryService.GetRecentInteractionsAsync(
            deviceId,
            count,
            cancellationToken);

        return Ok(interactions.Select(i => new InteractionDto
        {
            Id = i.Id,
            UserMessage = i.UserMessage,
            AssistantResponse = i.AssistantResponse,
            Timestamp = i.Timestamp,
            Commands = i.Commands.Select(c => new ExecutedCommandDto
            {
                Command = c.Command,
                Output = c.Output,
                Success = c.Success,
                ErrorMessage = c.ErrorMessage,
                ExecutedAt = c.ExecutedAt
            }).ToList()
        }).ToList());
    }

    /// <summary>
    /// Submit feedback for an interaction
    /// </summary>
    /// <param name="interactionId">Interaction ID</param>
    /// <param name="feedback">Feedback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("feedback/{interactionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> SubmitFeedback(
        string interactionId,
        [FromBody] FeedbackDto feedback,
        CancellationToken cancellationToken)
    {
        // TODO: Retrieve interaction, update feedback, add to knowledge base if positive

        _logger.LogInformation(
            "Received feedback for interaction {Id}: {Rating}/5",
            interactionId,
            feedback.Rating);

        return Task.FromResult<IActionResult>(Ok());
    }

    /// <summary>
    /// Get agent-specific chat history
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="count">Number of messages to retrieve</param>
    /// <param name="sessionId">Optional session ID filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of agent chat messages</returns>
    [HttpGet("agent/{agentId}/history")]
    [ProducesResponseType(typeof(List<AgentChatHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AgentChatHistoryDto>>> GetAgentChatHistory(
        string agentId,
        [FromQuery] int count = 50,
        [FromQuery] string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var chatHistory = await _agentChatHistoryService.GetChatHistoryAsync(
                agentId,
                skip: 0,
                take: count,
                sessionId: sessionId,
                cancellationToken: cancellationToken);

            var response = chatHistory.Select(c => new AgentChatHistoryDto
            {
                Id = c.Id,
                AgentId = c.AgentId,
                SessionId = c.SessionId,
                Role = c.Role,
                Content = c.Content,
                Commands = c.Commands,
                Timestamp = c.Timestamp,
                TokensUsed = c.TokensUsed,
                Success = c.Success
            }).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent chat history for {AgentId}", agentId);
            return StatusCode(500, new { message = "Failed to retrieve chat history" });
        }
    }

    /// <summary>
    /// Get agent chat statistics
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Agent chat statistics</returns>
    [HttpGet("agent/{agentId}/statistics")]
    [ProducesResponseType(typeof(AgentChatStatistics), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentChatStatistics>> GetAgentChatStatistics(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = await _agentChatHistoryService.GetChatStatisticsAsync(
                agentId,
                cancellationToken);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent chat statistics for {AgentId}", agentId);
            return StatusCode(500, new { message = "Failed to retrieve chat statistics" });
        }
    }

    /// <summary>
    /// Converts ChatMessage list (from SessionContextManager) to Interaction format for cascade orchestration
    /// </summary>
    private List<Interaction> ConvertChatMessagesToInteractions(List<ChatMessage> messages)
    {
        var interactions = new List<Interaction>();

        // Group consecutive user/assistant messages into interactions
        for (int i = 0; i < messages.Count; i++)
        {
            var current = messages[i];

            if (current.Role == "user")
            {
                // Find the next assistant message
                var assistantMsg = i + 1 < messages.Count && messages[i + 1].Role == "assistant"
                    ? messages[i + 1]
                    : null;

                interactions.Add(new Interaction
                {
                    Id = Guid.NewGuid().ToString(),
                    UserMessage = current.Content,
                    AssistantResponse = assistantMsg?.Content ?? string.Empty,
                    Timestamp = current.Timestamp
                });

                // Skip the assistant message since we've already processed it
                if (assistantMsg != null)
                {
                    i++;
                }
            }
        }

        return interactions;
    }

    /// <summary>
    /// Converts AgentChatHistory messages to Interaction format for cascade orchestration
    /// </summary>
    private List<Interaction> ConvertAgentChatHistoryToInteractions(List<AgentChatHistory> chatHistory)
    {
        var interactions = new List<Interaction>();

        // Group consecutive user/assistant messages into interactions
        for (int i = 0; i < chatHistory.Count; i++)
        {
            var current = chatHistory[i];

            if (current.Role == "user")
            {
                // Find the next assistant message
                var assistantMsg = i + 1 < chatHistory.Count && chatHistory[i + 1].Role == "assistant"
                    ? chatHistory[i + 1]
                    : null;

                interactions.Add(new Interaction
                {
                    Id = current.Id,
                    DeviceId = current.AgentId,
                    UserMessage = current.Content,
                    AssistantResponse = assistantMsg?.Content ?? string.Empty,
                    Timestamp = current.Timestamp
                });

                // Skip the assistant message since we've already processed it
                if (assistantMsg != null)
                {
                    i++;
                }
            }
        }

        return interactions;
    }

    /// <summary>
    /// TEST ENDPOINT - Simple ping to verify controller is loaded
    /// </summary>
    [HttpGet("ping")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Ping()
    {
        _logger.LogWarning("=== PING ENDPOINT HIT === ChatController is definitely loaded!");
        return Ok(new { message = "ChatController is alive!", timestamp = DateTime.UtcNow });
    }
}
