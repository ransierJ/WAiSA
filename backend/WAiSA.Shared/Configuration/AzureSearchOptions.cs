namespace WAiSA.Shared.Configuration;

/// <summary>
/// Configuration options for Azure AI Search
/// </summary>
public class AzureSearchOptions
{
    public const string SectionName = "AzureSearch";

    /// <summary>
    /// Azure AI Search service endpoint
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Azure AI Search admin key (retrieved from Key Vault)
    /// </summary>
    public string AdminKey { get; set; } = string.Empty;

    /// <summary>
    /// Knowledge base index name
    /// </summary>
    public string KnowledgeBaseIndexName { get; set; } = "knowledge-base-index";

    /// <summary>
    /// Number of top results to retrieve for RAG
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Minimum similarity score threshold (0.0 to 1.0)
    /// </summary>
    public double MinimumScore { get; set; } = 0.7;

    /// <summary>
    /// Vector search algorithm dimensions
    /// </summary>
    public int VectorDimensions { get; set; } = 1536; // text-embedding-3-large
}
