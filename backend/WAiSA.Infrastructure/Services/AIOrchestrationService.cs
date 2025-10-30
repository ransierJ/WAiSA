using Azure;
using Azure.AI.OpenAI;
using WAiSA.Core.Interfaces;
using WAiSA.Shared.Configuration;
using WAiSA.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// AI Orchestration Service implementation using Azure OpenAI
/// </summary>
public class AIOrchestrationService : IAIOrchestrationService
{
    private readonly OpenAIClient _openAIClient;
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<AIOrchestrationService> _logger;
    private readonly ISearchService _searchService;

    public AIOrchestrationService(
        OpenAIClient openAIClient,
        IOptions<AzureOpenAIOptions> options,
        ILogger<AIOrchestrationService> logger,
        ISearchService searchService)
    {
        _openAIClient = openAIClient;
        _options = options.Value;
        _logger = logger;
        _searchService = searchService;
    }

    /// <summary>
    /// Process a user message and generate AI response with function calling
    /// </summary>
    public async Task<ChatResponse> ProcessMessageAsync(
        string deviceId,
        string userMessage,
        List<Interaction>? recentInteractions = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Processing message for device {DeviceId}: {Message}",
                deviceId,
                userMessage);

            // Add MCP function definitions for documentation access
            var functionDefinitions = McpFunctionDefinitions.GetFunctionDefinitions();

            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                DeploymentName = _options.ChatDeploymentName,
                Messages =
                {
                    new ChatRequestSystemMessage(@"You are a helpful Windows System Administrator assistant with a conversational, friendly tone.

Your role: You manage Windows computers remotely and help users by executing PowerShell commands on their behalf, then explaining the results in natural, conversational language.

CRITICAL - CONVERSATION MEMORY (HIGHEST PRIORITY):
You MUST maintain conversation context and memory across messages. When users make conversational statements like:
- ""Remember this number: 42"" → Respond: ""Got it, I'll remember 42!""
- ""My name is Alice"" → Respond: ""Nice to meet you, Alice!""
- ""The error code was 0x8007001F"" → Respond: ""Noted, error code 0x8007001F. Let me look into that.""
- ""What number did I tell you?"" → Check conversation history and recall the number

NEVER treat these conversational statements as search queries. These are context-setting messages that should be acknowledged and remembered.

CRITICAL - YOU HAVE DIRECT ACCESS:
You have DIRECT, IMMEDIATE access to execute PowerShell commands on the target Windows computer through an automated command queueing system.
- NEVER ask for credentials, permissions, or access to the system
- NEVER say ""I need access to..."" or ""Please provide permissions...""
- NEVER say ""I cannot connect to..."" or ""I don't have access to...""
- You already have all the access you need - just write the PowerShell commands in code blocks
- The system automatically executes your commands and returns the results to you
- After research, IMMEDIATELY provide the diagnostic commands you want to run

CRITICAL - RESEARCH BEFORE ACTING:
When a user reports a problem or asks about troubleshooting, you MUST research BEFORE running commands:
1. FIRST: Search Microsoft Learn documentation for official solutions and best practices
2. SECOND: Check the Knowledge Base for similar issues (if available)
3. THIRD: Consider what diagnostic commands would be most helpful
4. THEN: Execute appropriate commands to diagnose the issue

Example flow for ""Adobe Acrobat is crashing"":
1. Call search_microsoft_docs to research Adobe Acrobat crash troubleshooting
2. Call search_microsoft_docs for Windows application crash diagnostics
3. Based on research, run appropriate diagnostic commands (event logs, app logs, etc.)
4. Analyze results and provide solution

NEVER immediately run commands without researching the issue first. Research helps you:
- Use the correct command syntax
- Know what to look for
- Provide accurate solutions
- Avoid trial-and-error

AFTER RESEARCH - IMMEDIATELY EXECUTE COMMANDS:
After you finish researching with search_microsoft_docs or search_web:
1. IMMEDIATELY provide diagnostic PowerShell commands in COMPLETE code blocks
2. DO NOT say ""I need to run..."" or ""Let me execute..."" - JUST PROVIDE THE COMMANDS
3. The commands will auto-execute - you'll get results back automatically
4. THEN interpret the results conversationally for the user

Example: After researching Windows crashes, IMMEDIATELY respond with:
```powershell
Get-EventLog -LogName System -EntryType Error -Newest 20
```
(NOT ""Here's the command I'll run..."" - just the code block, which auto-executes)

IMPORTANT - DOCUMENTATION AND WEB SEARCH ACCESS:
You have access to documentation through these functions with PRIORITY ORDER:

1. **search_microsoft_docs** - ALWAYS use this FIRST for official Microsoft solutions
   - Searches Microsoft Learn for Windows, PowerShell, Azure, .NET topics
   - Returns official, trusted documentation from Microsoft
   - This is your PRIMARY source for troubleshooting

2. **search_web** - Use this AFTER Microsoft docs for additional context
   - Searches the broader web for community solutions, forums, blogs
   - Use when Microsoft docs don't provide enough information
   - Helps find real-world examples and community workarounds

SEARCH WORKFLOW - ALWAYS follow this order:
1. Call search_microsoft_docs FIRST for official Microsoft solutions
2. If needed, call search_web for community insights and additional context
3. Combine findings from both sources in your diagnosis

ALWAYS use these search functions when:
- User reports a problem or crash
- User asks ""how do I..."" questions about Windows/PowerShell
- You need to verify command syntax or parameters
- User needs examples or best practices
- You're unsure about a specific feature or configuration
- BEFORE running diagnostic commands for any issue

CRITICAL - CONVERSATION HISTORY USAGE:
You receive conversation history in your context. ALWAYS read and use it to:
1. Answer questions about previous messages (""What number did I tell you?"" → Check history for the number)
2. Understand context and follow-up questions
3. Reference previous discussions, issues, or information
4. Maintain continuity across the conversation

HANDLING AFFIRMATIVE RESPONSES (""yes"", ""yes please"", ""sure"", ""ok"", ""do it""):
When user gives a simple affirmative response:
1. CHECK CONVERSATION HISTORY to find your most recent question or offer
2. If you offered ""Would you like me to help troubleshoot these errors?"" and user says ""yes please"", then TROUBLESHOOT THOSE SPECIFIC ERRORS you just showed them
3. If you offered ""Should I investigate?"" and user says ""yes"", then INVESTIGATE what you just mentioned
4. NEVER generate nonsense commands - use conversation history to understand what the user is agreeing to
5. If you can't determine what they're agreeing to from conversation history, ask for clarification

SPECIFIC EXAMPLES:
- If user says ""help troubleshoot these errors"" and you just showed them event log errors, help troubleshoot THOSE SPECIFIC ERRORS
- If user says ""yes dive deeper"" or ""tell me more"", refer to what you just discussed
- If user asks ""what about that?"" use conversation history to know what ""that"" refers to
- NEVER ask ""what errors?"" if you just showed them errors in the previous message
- NEVER ask for clarification on something you already discussed
- NEVER blame the user for a bad command that YOU generated

COMMAND GENERATION RULES - CRITICAL FOR AUTO-EXECUTION:
When you need to run diagnostic commands or execute PowerShell, you MUST format them EXACTLY like this:

CORRECT FORMAT (commands auto-execute):
```powershell
Get-EventLog -LogName System -EntryType Error -Newest 20
```

CRITICAL REQUIREMENTS:
1. Opening triple backticks MUST have 'powershell' tag: ```powershell
2. Command on NEW LINE after opening backticks
3. MUST have closing triple backticks: ```
4. NO explanatory text before, inside, or around the code block
5. Code block must be COMPLETE - both opening and closing backticks required

WRONG FORMATS (commands will NOT execute):
❌ ""Here's the command: ```powershell Get-EventLog..."" (missing closing backticks)
❌ ""Let me run: Get-EventLog..."" (no code block at all)
❌ ""I'll check the logs```powershell..."" (text before code block)
❌ ""```Get-EventLog...```"" (missing 'powershell' tag)

REMEMBER: Just provide the code block - nothing else. You'll interpret results after execution.

RESULT INTERPRETATION (when command output is provided):
After commands execute, respond conversationally like a helpful colleague:
- ✅ ""The system reported the date as Friday, October 24, 2025 11:56:21 AM""
- ✅ ""I found 3 errors in the event log. Would you like me to help resolve them?""
- ✅ ""The C: drive has 180 GB free out of 237 GB total - looking healthy!""
- ✅ ""The top process using CPU is SNAPOSS64 at 23,149 seconds. That seems unusual - shall I investigate?""

NEVER say:
- ❌ ""Here's the command...""
- ❌ ""The output shows...""
- ❌ ""Execute this PowerShell...""
- ❌ ""Command executed successfully""

CRITICAL - AUTOMATIC RETRY ON ERRORS (HIGHEST PRIORITY - NEVER SKIP THIS):
When ANY command fails, you MUST IMMEDIATELY retry with a fixed command - NO EXCEPTIONS:

MANDATORY RETRY PROCESS:
1. IMMEDIATELY analyze the error (syntax error, missing quotes, wrong parameter, etc.)
2. IMMEDIATELY generate a corrected PowerShell command
3. IMMEDIATELY provide ONLY the corrected command in a COMPLETE ```powershell code block (opening AND closing backticks)
4. DO NOT say ""there was an error"" or ""there was a small issue"" - JUST PROVIDE THE FIXED COMMAND
5. DO NOT explain what went wrong - JUST PROVIDE THE FIXED COMMAND
6. DO NOT apologize - JUST PROVIDE THE FIXED COMMAND
7. DO NOT say ""Let me fix that"" - JUST PROVIDE THE FIXED COMMAND
8. Retry up to 3 times before explaining the issue to the user

CORRECT behavior example (Adobe Acrobat crash):
Command fails with: ""Missing terminator quote""
✅ YOU IMMEDIATELY RESPOND WITH ONLY THIS (COMPLETE code block with BOTH opening and closing backticks):
```powershell
Get-EventLog -LogName Application -Source ""Adobe Acrobat"" -EntryType Error -Newest 10
```
(NO explanation, NO apology, just the COMPLETE fixed command in a COMPLETE code block - it will auto-execute)

WRONG behavior examples - NEVER DO THIS:
❌ ""It looks like there was a small issue with the command I tried to run...""
❌ ""Let me fix that and take another look...""
❌ ""I'll get back to you shortly.""
❌ ""```powershell Get-EventLog..."" (missing closing backticks - command won't execute!)
THIS IS COMPLETELY UNACCEPTABLE - The user sees this as you giving up!

CRITICAL: The code block MUST be COMPLETE with closing ``` or the command will NOT execute!
REMEMBER: Errors are INVISIBLE to the user. Just fix and retry silently with COMPLETE code blocks!

BE PROACTIVE:
- Offer to help fix problems you discover
- Use conversation history to understand follow-up questions
- Suggest next steps
- Sound like a helpful human colleague, not a robot

Keep responses SHORT and conversational (2-3 sentences max unless explaining something complex).")
                },
                MaxTokens = _options.MaxTokens,
                Temperature = (float)_options.Temperature,
                ToolChoice = ChatCompletionsToolChoice.Auto,
            };

            // Add recent conversation history for context (if provided)
            if (recentInteractions != null && recentInteractions.Any())
            {
                _logger.LogInformation(
                    "Adding {Count} recent interactions to conversation history for context",
                    recentInteractions.Count);

                foreach (var interaction in recentInteractions.OrderBy(i => i.Timestamp))
                {
                    // Add user message from history
                    chatCompletionsOptions.Messages.Add(
                        new ChatRequestUserMessage(interaction.UserMessage));

                    // Add assistant response from history
                    chatCompletionsOptions.Messages.Add(
                        new ChatRequestAssistantMessage(interaction.AssistantResponse));
                }
            }

            // Add current user message
            chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(userMessage));

            // Add MCP function definitions
            foreach (var functionDef in functionDefinitions)
            {
                chatCompletionsOptions.Tools.Add(new ChatCompletionsFunctionToolDefinition(functionDef));
            }

            // Activity logging for transparency
            var activityLogs = new List<ActivityLog>();
            activityLogs.Add(new ActivityLog
            {
                Type = "Start",
                Message = "Processing your request...",
                Icon = "🤖"
            });

            // Function calling loop - allow AI to call MCP functions as needed
            var conversationMessages = new List<ChatRequestMessage>(chatCompletionsOptions.Messages);
            string? finalResponse = null;
            int maxIterations = 5;
            int iteration = 0;

            while (iteration < maxIterations)
            {
                iteration++;

                var response = await _openAIClient.GetChatCompletionsAsync(
                    chatCompletionsOptions,
                    cancellationToken);

                var choice = response.Value.Choices.FirstOrDefault();
                if (choice == null)
                {
                    return new ChatResponse
                    {
                        Success = false,
                        ErrorMessage = "No response from AI service"
                    };
                }

                // Check if AI wants to call a function
                if (choice.FinishReason == CompletionsFinishReason.ToolCalls && choice.Message.ToolCalls?.Count > 0)
                {
                    _logger.LogInformation("AI requested {Count} function calls", choice.Message.ToolCalls.Count);

                    // Add assistant message with tool calls to conversation
                    conversationMessages.Add(new ChatRequestAssistantMessage(choice.Message));

                    // Execute each function call
                    foreach (var toolCall in choice.Message.ToolCalls)
                    {
                        if (toolCall is ChatCompletionsFunctionToolCall functionToolCall)
                        {
                            // Log the function call activity
                            var (icon, message) = GetActivityInfoForFunction(functionToolCall.Name);
                            activityLogs.Add(new ActivityLog
                            {
                                Type = "FunctionCall",
                                Message = message,
                                Icon = icon,
                                Details = functionToolCall.Arguments
                            });

                            var functionResult = await ExecuteMcpFunctionAsync(
                                functionToolCall.Name,
                                functionToolCall.Arguments,
                                cancellationToken);

                            // Add function result to conversation
                            conversationMessages.Add(new ChatRequestToolMessage(functionResult, functionToolCall.Id));
                        }
                    }

                    // Update messages for next iteration
                    chatCompletionsOptions.Messages.Clear();
                    foreach (var msg in conversationMessages)
                    {
                        chatCompletionsOptions.Messages.Add(msg);
                    }
                }
                else
                {
                    // No more function calls - we have the final response
                    finalResponse = choice.Message.Content;

                    // Log completion
                    activityLogs.Add(new ActivityLog
                    {
                        Type = "Complete",
                        Message = "Response ready",
                        Icon = "✅"
                    });

                    _logger.LogInformation(
                        "Final response generated for device {DeviceId}. Tokens used: {Tokens}, Iterations: {Iterations}",
                        deviceId,
                        response.Value.Usage.TotalTokens,
                        iteration);
                    break;
                }
            }

            if (finalResponse == null)
            {
                return new ChatResponse
                {
                    Success = false,
                    ErrorMessage = "Max function calling iterations reached"
                };
            }

            return new ChatResponse
            {
                Message = finalResponse,
                Success = true,
                TokensUsed = 0, // Would need to track across all iterations
                ExecutedCommands = new List<ExecutedCommand>(),
                ActivityLogs = activityLogs
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message for device {DeviceId}", deviceId);
            return new ChatResponse
            {
                Success = false,
                ErrorMessage = $"Error processing message: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generate vector embedding for text content
    /// </summary>
    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddingsOptions = new EmbeddingsOptions(_options.EmbeddingDeploymentName, new[] { text });

            var response = await _openAIClient.GetEmbeddingsAsync(
                embeddingsOptions,
                cancellationToken);

            var embedding = response.Value.Data.FirstOrDefault();
            if (embedding == null)
            {
                _logger.LogWarning("No embedding returned for text");
                return Array.Empty<float>();
            }

            return embedding.Embedding.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding");
            return Array.Empty<float>();
        }
    }

    /// <summary>
    /// Summarize device context from recent interactions
    /// </summary>
    public async Task<string> SummarizeContextAsync(
        List<Interaction> interactions,
        int maxTokens = 500,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!interactions.Any())
            {
                return string.Empty;
            }

            // Build interaction history for summarization
            var interactionText = string.Join("\n\n",
                interactions.Select(i =>
                    $"User: {i.UserMessage}\nAssistant: {i.AssistantResponse}"));

            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                DeploymentName = _options.ChatDeploymentName,
                Messages =
                {
                    new ChatRequestSystemMessage(@"You are an AI summarization assistant.
Your task is to create a concise context summary of recent Windows system administration interactions.

Create a summary that includes:
1. Key system issues or requests
2. Actions taken or commands executed
3. Important system state or configuration details
4. Any ongoing concerns or follow-up items

Keep the summary under 500 tokens and focus on information that would be helpful for future interactions.
Be specific about technical details (software versions, configurations, error messages, etc.)"),
                    new ChatRequestUserMessage($@"Summarize these recent interactions:

{interactionText}

Provide a concise summary in {maxTokens} tokens or less.")
                },
                MaxTokens = maxTokens,
                Temperature = 0.3f // Lower temperature for more focused summarization
            };

            var response = await _openAIClient.GetChatCompletionsAsync(
                chatCompletionsOptions,
                cancellationToken);

            var choice = response.Value.Choices.FirstOrDefault();
            var summary = choice?.Message.Content ?? string.Empty;

            _logger.LogInformation(
                "Generated context summary. Input interactions: {Count}, Tokens used: {Tokens}",
                interactions.Count,
                response.Value.Usage.TotalTokens);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error summarizing context");
            return string.Empty;
        }
    }

    /// <summary>
    /// Execute an MCP function call
    /// </summary>
    private async Task<string> ExecuteMcpFunctionAsync(
        string functionName,
        string arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Executing MCP function: {FunctionName} with args: {Arguments}", functionName, arguments);

            return functionName switch
            {
                "search_microsoft_docs" => await ExecuteSearchDocsAsync(arguments, cancellationToken),
                "search_web" => await ExecuteSearchWebAsync(arguments, cancellationToken),
                _ => JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing MCP function {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> ExecuteSearchDocsAsync(string arguments, CancellationToken cancellationToken)
    {
        var args = McpFunctionDefinitions.ParseFunctionArguments<SearchDocsArguments>(arguments);
        if (args == null || string.IsNullOrWhiteSpace(args.Query))
        {
            return JsonSerializer.Serialize(new { error = "Invalid arguments" });
        }

        var result = await _searchService.SearchMicrosoftDocsAsync(
            args.Query,
            args.MaxResults ?? 5,
            cancellationToken);

        return JsonSerializer.Serialize(result);
    }

    private async Task<string> ExecuteSearchWebAsync(string arguments, CancellationToken cancellationToken)
    {
        var args = McpFunctionDefinitions.ParseFunctionArguments<WebSearchArguments>(arguments);
        if (args == null || string.IsNullOrWhiteSpace(args.Query))
        {
            return JsonSerializer.Serialize(new { error = "Invalid arguments" });
        }

        var result = await _searchService.SearchWebAsync(
            args.Query,
            args.MaxResults ?? 5,
            cancellationToken);

        return JsonSerializer.Serialize(result);
    }

    /// <summary>
    /// Get activity icon and message for a function call
    /// </summary>
    private (string icon, string message) GetActivityInfoForFunction(string functionName)
    {
        return functionName switch
        {
            "search_microsoft_docs" => ("🔍", "Searching Microsoft Learn documentation..."),
            "search_web" => ("🌐", "Searching the web for additional solutions..."),
            _ => ("🔧", $"Calling {functionName}...")
        };
    }
}
