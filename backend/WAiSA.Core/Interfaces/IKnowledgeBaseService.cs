using WAiSA.Shared.Models;

namespace WAiSA.Core.Interfaces;

/// <summary>
/// Knowledge Base Service for RAG (Retrieval Augmented Generation)
/// Manages organizational knowledge shared across all devices
/// </summary>
public interface IKnowledgeBaseService
{
    /// <summary>
    /// Add or update knowledge entry in the knowledge base
    /// </summary>
    /// <param name="entry">Knowledge entry to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddOrUpdateKnowledgeAsync(
        KnowledgeBaseEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve relevant knowledge for a query using vector search
    /// </summary>
    /// <param name="query">User query</param>
    /// <param name="topK">Number of top results to retrieve (default 5)</param>
    /// <param name="minScore">Minimum similarity score threshold (default 0.7)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of relevant knowledge entries</returns>
    Task<List<KnowledgeSearchResult>> RetrieveRelevantKnowledgeAsync(
        string query,
        int topK = 5,
        double minScore = 0.7,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract and store knowledge from a successful interaction
    /// Called when user provides positive feedback
    /// </summary>
    /// <param name="interaction">Interaction to extract knowledge from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created knowledge entry</returns>
    Task<KnowledgeBaseEntry> ExtractKnowledgeFromInteractionAsync(
        Interaction interaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search knowledge base with filters
    /// </summary>
    /// <param name="searchText">Search text</param>
    /// <param name="tags">Filter by tags (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching knowledge entries</returns>
    Task<List<KnowledgeBaseEntry>> SearchKnowledgeAsync(
        string searchText,
        List<string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all knowledge entries (paginated)
    /// </summary>
    /// <param name="skip">Number of entries to skip</param>
    /// <param name="take">Number of entries to take</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of knowledge entries</returns>
    Task<List<KnowledgeBaseEntry>> GetAllKnowledgeAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Record knowledge usage and update statistics
    /// </summary>
    /// <param name="knowledgeId">Knowledge entry ID</param>
    /// <param name="rating">User rating (1-5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecordKnowledgeUsageAsync(
        string knowledgeId,
        int? rating = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete knowledge entry
    /// </summary>
    /// <param name="knowledgeId">Knowledge entry ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteKnowledgeAsync(
        string knowledgeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initialize the Azure AI Search index for knowledge base
    /// Creates the index if it doesn't exist
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeIndexAsync(CancellationToken cancellationToken = default);
}
