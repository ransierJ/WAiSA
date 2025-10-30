using WAiSA.Shared.Models;

namespace WAiSA.Core.Interfaces;

/// <summary>
/// AI Orchestration Service for handling chat interactions with Azure OpenAI
/// </summary>
public interface IAIOrchestrationService
{
    /// <summary>
    /// Process a user message and generate AI response with function calling
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="userMessage">User's message/request</param>
    /// <param name="recentInteractions">Optional recent conversation history for context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI assistant response with executed commands</returns>
    Task<ChatResponse> ProcessMessageAsync(
        string deviceId,
        string userMessage,
        List<Interaction>? recentInteractions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate vector embedding for text content
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Vector embedding (1536 dimensions)</returns>
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Summarize device context from recent interactions
    /// </summary>
    /// <param name="interactions">List of recent interactions</param>
    /// <param name="maxTokens">Maximum tokens for summary (default 500)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI-generated context summary</returns>
    Task<string> SummarizeContextAsync(
        List<Interaction> interactions,
        int maxTokens = 500,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Chat response from AI orchestration
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// AI assistant response text
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Commands executed during the interaction
    /// </summary>
    public List<ExecutedCommand> ExecutedCommands { get; set; } = new();

    /// <summary>
    /// Activity logs tracking AI's working process
    /// </summary>
    public List<ActivityLog> ActivityLogs { get; set; } = new();

    /// <summary>
    /// Whether the interaction was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if interaction failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Total tokens used in the interaction
    /// </summary>
    public int TokensUsed { get; set; }
}

/// <summary>
/// Represents a single activity log entry during AI processing
/// </summary>
public class ActivityLog
{
    /// <summary>
    /// Timestamp when this activity occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Type of activity (Research, FunctionCall, CommandExecution, Retry, Complete, Error)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable message describing the activity
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Icon to display for this activity (🔍, 📚, 💻, ✅, ❌, 🔄)
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Optional detailed information (e.g., function arguments, error details)
    /// </summary>
    public string? Details { get; set; }
}
