using WAiSA.Shared.Models;

namespace WAiSA.Core.Interfaces;

/// <summary>
/// Cascading Search Service for multi-tier knowledge retrieval with fallback strategy.
/// Implements tiered search pattern: Cache → Knowledge Base → External API
/// </summary>
public interface ICascadingSearchService
{
    /// <summary>
    /// Executes cascading search across multiple tiers with automatic fallback.
    /// Tiers are executed sequentially with timeout and circuit breaker protection:
    /// - Tier 1: Local memory cache (fastest, sub-10ms)
    /// - Tier 2: Azure AI Search knowledge base (fast, 100-500ms)
    /// - Tier 3: External API fallback (slow, 1-5s, disabled by default)
    /// </summary>
    /// <param name="query">User query string for semantic search</param>
    /// <param name="topK">Maximum number of results to return (default 3)</param>
    /// <param name="minScore">Minimum similarity score threshold (0.0-1.0, default 0.7)</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation</param>
    /// <returns>
    /// Aggregated search results from successful tiers with metadata about which tier provided results
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested</exception>
    Task<CascadingSearchResult> SearchAsync(
        string query,
        int topK = 3,
        double minScore = 0.7,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Warms up the cache tier by pre-loading frequently accessed knowledge entries.
    /// Should be called during application startup or periodically.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WarmupCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the cache tier, forcing fresh retrieval from lower tiers.
    /// Useful for testing or after knowledge base updates.
    /// </summary>
    Task ClearCacheAsync();

    /// <summary>
    /// Gets health status and metrics for each tier including:
    /// - Circuit breaker state (Open/HalfOpen/Closed)
    /// - Recent success/failure rates
    /// - Average response times
    /// - Cache hit/miss statistics
    /// </summary>
    /// <returns>Health status for all tiers</returns>
    Task<TierHealthStatus> GetHealthStatusAsync();
}
