namespace WAiSA.Shared.Configuration;

/// <summary>
/// Configuration options for Azure Cosmos DB
/// </summary>
public class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    /// <summary>
    /// Cosmos DB account endpoint
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Cosmos DB account key (retrieved from Key Vault)
    /// </summary>
    public string AccountKey { get; set; } = string.Empty;

    /// <summary>
    /// Database name
    /// </summary>
    public string DatabaseName { get; set; } = "WAiSA";

    /// <summary>
    /// Device memory container name
    /// </summary>
    public string DeviceMemoryContainer { get; set; } = "DeviceMemory";

    /// <summary>
    /// Interaction history container name
    /// </summary>
    public string InteractionHistoryContainer { get; set; } = "InteractionHistory";

    /// <summary>
    /// Context snapshots container name
    /// </summary>
    public string ContextSnapshotsContainer { get; set; } = "ContextSnapshots";

    /// <summary>
    /// Agent chat history container name (for audit)
    /// </summary>
    public string AgentChatHistoryContainer { get; set; } = "AgentChatHistory";

    /// <summary>
    /// Maximum number of interactions before triggering summarization
    /// </summary>
    public int SummarizationThreshold { get; set; } = 20;

    /// <summary>
    /// Maximum tokens for device context summary
    /// </summary>
    public int MaxSummaryTokens { get; set; } = 500;
}
