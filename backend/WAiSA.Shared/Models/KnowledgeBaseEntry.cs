using System.Text.Json.Serialization;

namespace WAiSA.Shared.Models;

/// <summary>
/// Represents a knowledge base entry for RAG (Retrieval Augmented Generation)
/// Stored in Azure AI Search with vector embeddings
/// </summary>
public class KnowledgeBaseEntry
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Knowledge entry title/summary
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Full knowledge content
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Source of the knowledge (interaction, documentation, etc.)
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Source device ID (if from a specific device interaction)
    /// </summary>
    [JsonPropertyName("sourceDeviceId")]
    public string? SourceDeviceId { get; set; }

    /// <summary>
    /// Category/tags for the knowledge entry
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Vector embedding for semantic search (1536 dimensions for text-embedding-3-large)
    /// </summary>
    [JsonPropertyName("contentVector")]
    public float[] ContentVector { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Number of times this knowledge was retrieved/used
    /// </summary>
    [JsonPropertyName("usageCount")]
    public int UsageCount { get; set; }

    /// <summary>
    /// Average user rating for this knowledge (when used)
    /// </summary>
    [JsonPropertyName("averageRating")]
    public double? AverageRating { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time this knowledge was retrieved/used
    /// </summary>
    [JsonPropertyName("lastUsedAt")]
    public DateTime? LastUsedAt { get; set; }
}
