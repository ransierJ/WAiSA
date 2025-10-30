namespace WAiSA.Shared.Configuration;

/// <summary>
/// Configuration options for cascading search service
/// </summary>
public class CascadingSearchOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "CascadingSearch";

    /// <summary>
    /// Tier 1 (Cache) configuration
    /// </summary>
    public TierConfiguration Tier1Cache { get; set; } = new()
    {
        Enabled = true,
        TimeoutMs = 100,
        Name = "MemoryCache"
    };

    /// <summary>
    /// Tier 2 (Knowledge Base) configuration
    /// </summary>
    public TierConfiguration Tier2KnowledgeBase { get; set; } = new()
    {
        Enabled = true,
        TimeoutMs = 2000,
        Name = "AzureAISearch"
    };

    /// <summary>
    /// Tier 3 (External API) configuration
    /// </summary>
    public TierConfiguration Tier3ExternalAPI { get; set; } = new()
    {
        Enabled = false, // Disabled by default
        TimeoutMs = 5000,
        Name = "ExternalAPI",
        ApiEndpoint = "https://api.example.com/search"
    };

    /// <summary>
    /// Circuit breaker configuration
    /// </summary>
    public CircuitBreakerConfiguration CircuitBreaker { get; set; } = new();

    /// <summary>
    /// Cache configuration
    /// </summary>
    public CacheConfiguration Cache { get; set; } = new();

    /// <summary>
    /// Maximum number of tiers to attempt before giving up
    /// </summary>
    public int MaxTiersToAttempt { get; set; } = 3;

    /// <summary>
    /// Whether to aggregate results from multiple tiers or stop at first success
    /// </summary>
    public bool AggregateResults { get; set; } = false;
}

/// <summary>
/// Configuration for a single tier
/// </summary>
public class TierConfiguration
{
    /// <summary>
    /// Whether this tier is enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Timeout for this tier in milliseconds
    /// </summary>
    public int TimeoutMs { get; set; }

    /// <summary>
    /// Tier display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// API endpoint (for external tiers)
    /// </summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>
    /// API key (for external tiers)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Retry configuration
    /// </summary>
    public RetryConfiguration Retry { get; set; } = new();
}

/// <summary>
/// Circuit breaker configuration using Polly library pattern
/// </summary>
public class CircuitBreakerConfiguration
{
    /// <summary>
    /// Number of consecutive failures before opening circuit
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Duration to keep circuit open (seconds)
    /// </summary>
    public int DurationOfBreakSeconds { get; set; } = 30;

    /// <summary>
    /// Minimum throughput before circuit breaker activates
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Sampling duration for failure rate calculation (seconds)
    /// </summary>
    public int SamplingDurationSeconds { get; set; } = 60;
}

/// <summary>
/// Cache configuration
/// </summary>
public class CacheConfiguration
{
    /// <summary>
    /// Cache entry expiration (minutes)
    /// </summary>
    public int ExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Maximum cache size (number of entries)
    /// </summary>
    public int MaxSize { get; set; } = 1000;

    /// <summary>
    /// Sliding expiration enabled
    /// </summary>
    public bool SlidingExpiration { get; set; } = true;
}

/// <summary>
/// Retry configuration
/// </summary>
public class RetryConfiguration
{
    /// <summary>
    /// Maximum retry attempts
    /// </summary>
    public int MaxAttempts { get; set; } = 2;

    /// <summary>
    /// Delay between retries (milliseconds)
    /// </summary>
    public int DelayMs { get; set; } = 100;

    /// <summary>
    /// Use exponential backoff
    /// </summary>
    public bool ExponentialBackoff { get; set; } = true;
}
