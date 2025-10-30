namespace WAiSA.Shared.Models;

/// <summary>
/// Result container for cascading search operations with tier attribution
/// </summary>
public class CascadingSearchResult
{
    /// <summary>
    /// Combined knowledge results from all successful tiers
    /// </summary>
    public List<KnowledgeSearchResult> Results { get; set; } = new();

    /// <summary>
    /// Metadata about which tier(s) provided results
    /// </summary>
    public CascadingSearchMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Overall success status
    /// </summary>
    public bool Success => Results.Any();

    /// <summary>
    /// Human-readable summary of the search execution
    /// Example: "Retrieved 3 results from Tier 1 (Cache) in 8ms"
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Metadata about cascading search execution
/// </summary>
public class CascadingSearchMetadata
{
    /// <summary>
    /// Which tier successfully provided results (Cache, KnowledgeBase, ExternalAPI)
    /// </summary>
    public string SuccessfulTier { get; set; } = string.Empty;

    /// <summary>
    /// Total execution time across all attempted tiers (milliseconds)
    /// </summary>
    public long TotalExecutionTimeMs { get; set; }

    /// <summary>
    /// Detailed timing per tier
    /// </summary>
    public Dictionary<string, TierExecutionInfo> TierExecutions { get; set; } = new();

    /// <summary>
    /// Whether cache was hit (Tier 1 success)
    /// </summary>
    public bool CacheHit { get; set; }

    /// <summary>
    /// Number of tiers attempted before success
    /// </summary>
    public int TiersAttempted { get; set; }
}

/// <summary>
/// Execution information for a single tier
/// </summary>
public class TierExecutionInfo
{
    /// <summary>
    /// Tier name (Tier1-Cache, Tier2-KnowledgeBase, Tier3-ExternalAPI)
    /// </summary>
    public string TierName { get; set; } = string.Empty;

    /// <summary>
    /// Execution time for this tier (milliseconds)
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Whether this tier succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of results returned by this tier
    /// </summary>
    public int ResultCount { get; set; }

    /// <summary>
    /// Error message if tier failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether circuit breaker was open (tier skipped)
    /// </summary>
    public bool CircuitBreakerOpen { get; set; }
}

/// <summary>
/// Health status for all tiers
/// </summary>
public class TierHealthStatus
{
    /// <summary>
    /// Overall health (Healthy, Degraded, Unhealthy)
    /// </summary>
    public string OverallHealth { get; set; } = "Healthy";

    /// <summary>
    /// Health status per tier
    /// </summary>
    public Dictionary<string, TierHealth> Tiers { get; set; } = new();
}

/// <summary>
/// Health metrics for a single tier
/// </summary>
public class TierHealth
{
    /// <summary>
    /// Tier name
    /// </summary>
    public string TierName { get; set; } = string.Empty;

    /// <summary>
    /// Circuit breaker state (Closed, Open, HalfOpen)
    /// </summary>
    public string CircuitBreakerState { get; set; } = "Closed";

    /// <summary>
    /// Success rate over last 100 requests (0.0-1.0)
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Average response time (milliseconds)
    /// </summary>
    public long AverageResponseTimeMs { get; set; }

    /// <summary>
    /// Cache hit rate (Tier 1 only)
    /// </summary>
    public double? CacheHitRate { get; set; }

    /// <summary>
    /// Total requests served
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Failed request count
    /// </summary>
    public long FailedRequests { get; set; }
}
