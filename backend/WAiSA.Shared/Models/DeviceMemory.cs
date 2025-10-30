namespace WAiSA.Shared.Models;

/// <summary>
/// Represents persistent memory for a specific Windows device
/// </summary>
public class DeviceMemory
{
    /// <summary>
    /// Unique identifier (Cosmos DB document ID)
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Device unique identifier (partition key)
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Device friendly name
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// AI-generated context summary (max 500 tokens)
    /// Updated every 20 interactions via summarization
    /// </summary>
    public string ContextSummary { get; set; } = string.Empty;

    /// <summary>
    /// Number of tokens in the context summary
    /// </summary>
    public int SummaryTokenCount { get; set; }

    /// <summary>
    /// Total number of interactions processed
    /// </summary>
    public int TotalInteractions { get; set; }

    /// <summary>
    /// Number of interactions since last summarization
    /// </summary>
    public int InteractionsSinceLastSummary { get; set; }

    /// <summary>
    /// Last interaction timestamp
    /// </summary>
    public DateTime LastInteractionAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last summarization timestamp
    /// </summary>
    public DateTime? LastSummarizedAt { get; set; }

    /// <summary>
    /// Device metadata (OS version, hostname, etc.)
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
