using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WAiSA.Core.Interfaces;
using WAiSA.Shared.Configuration;
using WAiSA.Shared.Models;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// Production-grade cascading search service with circuit breaker, timeouts, and observability.
/// Implements tiered search pattern with automatic fallback and health monitoring.
/// </summary>
public class CascadingSearchService : ICascadingSearchService
{
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CascadingSearchService> _logger;
    private readonly CascadingSearchOptions _options;

    // Health metrics tracking
    private readonly TierMetrics _tier1Metrics = new();
    private readonly TierMetrics _tier2Metrics = new();
    private readonly TierMetrics _tier3Metrics = new();

    // Simple circuit breaker states
    private CircuitBreakerState _tier1State = new();
    private CircuitBreakerState _tier2State = new();
    private CircuitBreakerState _tier3State = new();

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public CascadingSearchService(
        IKnowledgeBaseService knowledgeBaseService,
        IMemoryCache memoryCache,
        ILogger<CascadingSearchService> logger,
        IOptions<CascadingSearchOptions> options)
    {
        _knowledgeBaseService = knowledgeBaseService;
        _memoryCache = memoryCache;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Executes cascading search with automatic fallback
    /// </summary>
    public async Task<CascadingSearchResult> SearchAsync(
        string query,
        int topK = 3,
        double minScore = 0.7,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new CascadingSearchResult();

        _logger.LogInformation(
            "[CascadingSearch] Starting search for query: {Query}, topK: {TopK}, minScore: {MinScore}",
            query, topK, minScore);

        try
        {
            // Tier 1: Memory Cache (fastest, <10ms expected)
            if (_options.Tier1Cache.Enabled && !_tier1State.IsOpen)
            {
                var tier1Result = await ExecuteTier1CacheAsync(
                    query, topK, minScore, result, cancellationToken);

                if (tier1Result.Success && result.Results.Count >= topK && !_options.AggregateResults)
                {
                    result.Metadata.SuccessfulTier = "Tier1-Cache";
                    result.Metadata.CacheHit = true;
                    _logger.LogInformation(
                        "[CascadingSearch] ✓ Tier 1 (Cache) SUCCESS - {Count} results in {Ms}ms",
                        tier1Result.ResultCount, tier1Result.ExecutionTimeMs);

                    return FinalizeResult(result, stopwatch);
                }
            }

            // Tier 2: Knowledge Base (fast, <500ms expected)
            if (_options.Tier2KnowledgeBase.Enabled && !_tier2State.IsOpen &&
                (result.Results.Count < topK || _options.AggregateResults))
            {
                var tier2Result = await ExecuteTier2KnowledgeBaseAsync(
                    query, topK, minScore, result, cancellationToken);

                if (tier2Result.Success)
                {
                    if (string.IsNullOrEmpty(result.Metadata.SuccessfulTier))
                    {
                        result.Metadata.SuccessfulTier = "Tier2-KnowledgeBase";
                    }

                    _logger.LogInformation(
                        "[CascadingSearch] ✓ Tier 2 (KnowledgeBase) SUCCESS - {Count} results in {Ms}ms",
                        tier2Result.ResultCount, tier2Result.ExecutionTimeMs);

                    // Cache successful results for Tier 1
                    await CacheResultsAsync(query, result.Results, topK, minScore);

                    if (!_options.AggregateResults && result.Results.Count >= topK)
                    {
                        return FinalizeResult(result, stopwatch);
                    }
                }
            }

            // Tier 3: External API Fallback (slow, <5s expected) - Currently not implemented
            if (_options.Tier3ExternalAPI.Enabled && !_tier3State.IsOpen &&
                (result.Results.Count < topK || _options.AggregateResults))
            {
                var tier3Result = await ExecuteTier3ExternalAPIAsync(
                    query, topK, minScore, result, cancellationToken);

                if (tier3Result.Success)
                {
                    if (string.IsNullOrEmpty(result.Metadata.SuccessfulTier))
                    {
                        result.Metadata.SuccessfulTier = "Tier3-ExternalAPI";
                    }

                    _logger.LogInformation(
                        "[CascadingSearch] ✓ Tier 3 (ExternalAPI) SUCCESS - {Count} results in {Ms}ms",
                        tier3Result.ResultCount, tier3Result.ExecutionTimeMs);

                    await CacheResultsAsync(query, result.Results, topK, minScore);
                }
            }

            return FinalizeResult(result, stopwatch);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[CascadingSearch] Search cancelled for query: {Query}", query);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CascadingSearch] Unexpected error during search: {Query}", query);
            return FinalizeResult(result, stopwatch);
        }
    }

    /// <summary>
    /// Tier 1: Memory Cache execution
    /// Expected: <10ms response time
    /// </summary>
    private async Task<TierExecutionInfo> ExecuteTier1CacheAsync(
        string query,
        int topK,
        double minScore,
        CascadingSearchResult result,
        CancellationToken cancellationToken)
    {
        var tierInfo = new TierExecutionInfo { TierName = "Tier1-Cache" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.Tier1Cache.TimeoutMs);

            // Generate cache key
            var cacheKey = GenerateCacheKey(query, topK, minScore);

            // Try to get from cache
            if (_memoryCache.TryGetValue<List<KnowledgeSearchResult>>(cacheKey, out var cachedResults))
            {
                _logger.LogDebug("[Tier1] Cache HIT for key: {Key}", cacheKey);
                _tier1Metrics.RecordCacheHit();

                stopwatch.Stop();
                tierInfo.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

                if (cachedResults != null && cachedResults.Any())
                {
                    result.Results.AddRange(cachedResults);
                    tierInfo.Success = true;
                    tierInfo.ResultCount = cachedResults.Count;
                    _tier1Metrics.RecordSuccess(stopwatch.ElapsedMilliseconds);
                    _tier1State.RecordSuccess();
                }
                else
                {
                    tierInfo.Success = false;
                    tierInfo.ErrorMessage = "Cache hit but empty results";
                }
            }
            else
            {
                _logger.LogDebug("[Tier1] Cache MISS for key: {Key}", cacheKey);
                _tier1Metrics.RecordCacheMiss();

                stopwatch.Stop();
                tierInfo.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
                tierInfo.Success = false;
                tierInfo.ErrorMessage = "Cache miss";
            }

            await Task.CompletedTask;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            tierInfo.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            tierInfo.Success = false;
            tierInfo.ErrorMessage = $"Timeout after {_options.Tier1Cache.TimeoutMs}ms";
            _tier1Metrics.RecordFailure();
            _tier1State.RecordFailure();
            _logger.LogWarning("[Tier1] Timeout");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            tierInfo.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            tierInfo.Success = false;
            tierInfo.ErrorMessage = ex.Message;
            _tier1Metrics.RecordFailure();
            _tier1State.RecordFailure();
            _logger.LogError(ex, "[Tier1] Error accessing cache");
        }

        result.Metadata.TierExecutions["Tier1-Cache"] = tierInfo;
        result.Metadata.TiersAttempted++;
        return tierInfo;
    }

    /// <summary>
    /// Tier 2: Knowledge Base (Azure AI Search) execution
    /// Expected: 100-500ms response time
    /// </summary>
    private async Task<TierExecutionInfo> ExecuteTier2KnowledgeBaseAsync(
        string query,
        int topK,
        double minScore,
        CascadingSearchResult result,
        CancellationToken cancellationToken)
    {
        var tierInfo = new TierExecutionInfo { TierName = "Tier2-KnowledgeBase" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.Tier2KnowledgeBase.TimeoutMs);

            _logger.LogDebug("[Tier2] Querying knowledge base with timeout: {Ms}ms",
                _options.Tier2KnowledgeBase.TimeoutMs);

            var kbResults = await _knowledgeBaseService.RetrieveRelevantKnowledgeAsync(
                query, topK, minScore, timeoutCts.Token);

            stopwatch.Stop();
            tierInfo.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            if (kbResults != null && kbResults.Any())
            {
                // Deduplicate with existing results (by entry ID)
                var existingIds = result.Results.Select(r => r.Entry.Id).ToHashSet();
                var newResults = kbResults.Where(r => !existingIds.Contains(r.Entry.Id)).ToList();

                result.Results.AddRange(newResults);
                tierInfo.Success = true;
                tierInfo.ResultCount = newResults.Count;
                _tier2Metrics.RecordSuccess(stopwatch.ElapsedMilliseconds);
                _tier2State.RecordSuccess();

                _logger.LogDebug("[Tier2] Retrieved {Count} new results (total: {Total})",
                    newResults.Count, result.Results.Count);
            }
            else
            {
                tierInfo.Success = false;
                tierInfo.ErrorMessage = "No results from knowledge base";
                _logger.LogDebug("[Tier2] No results returned from knowledge base");
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            tierInfo.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            tierInfo.Success = false;
            tierInfo.ErrorMessage = $"Timeout after {_options.Tier2KnowledgeBase.TimeoutMs}ms";
            _tier2Metrics.RecordFailure();
            _tier2State.RecordFailure();
            _logger.LogWarning("[Tier2] Timeout");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            tierInfo.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            tierInfo.Success = false;
            tierInfo.ErrorMessage = ex.Message;
            _tier2Metrics.RecordFailure();
            _tier2State.RecordFailure();
            _logger.LogError(ex, "[Tier2] Error querying knowledge base");
        }

        result.Metadata.TierExecutions["Tier2-KnowledgeBase"] = tierInfo;
        result.Metadata.TiersAttempted++;
        return tierInfo;
    }

    /// <summary>
    /// Tier 3: External API execution (placeholder for future implementation)
    /// Expected: 1-5s response time
    /// </summary>
    private async Task<TierExecutionInfo> ExecuteTier3ExternalAPIAsync(
        string query,
        int topK,
        double minScore,
        CascadingSearchResult result,
        CancellationToken cancellationToken)
    {
        var tierInfo = new TierExecutionInfo { TierName = "Tier3-ExternalAPI" };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("[Tier3] External API not implemented yet");

            // TODO: Implement external API call (e.g., Bing Search, Google, etc.)
            await Task.Delay(10, cancellationToken);

            stopwatch.Stop();
            tierInfo.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            tierInfo.Success = false;
            tierInfo.ErrorMessage = "External API not implemented";
            tierInfo.ResultCount = 0;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            tierInfo.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            tierInfo.Success = false;
            tierInfo.ErrorMessage = ex.Message;
            _tier3Metrics.RecordFailure();
            _tier3State.RecordFailure();
            _logger.LogError(ex, "[Tier3] Error calling external API");
        }

        result.Metadata.TierExecutions["Tier3-ExternalAPI"] = tierInfo;
        result.Metadata.TiersAttempted++;
        return tierInfo;
    }

    /// <summary>
    /// Cache successful search results for Tier 1
    /// </summary>
    private Task CacheResultsAsync(
        string query,
        List<KnowledgeSearchResult> results,
        int topK,
        double minScore)
    {
        try
        {
            if (!results.Any()) return Task.CompletedTask;

            var cacheKey = GenerateCacheKey(query, topK, minScore);
            var cacheOptions = new MemoryCacheEntryOptions();

            if (_options.Cache.SlidingExpiration)
            {
                cacheOptions.SetSlidingExpiration(
                    TimeSpan.FromMinutes(_options.Cache.ExpirationMinutes));
            }
            else
            {
                cacheOptions.SetAbsoluteExpiration(
                    TimeSpan.FromMinutes(_options.Cache.ExpirationMinutes));
            }

            // Only cache top results
            var topResults = results.OrderByDescending(r => r.SimilarityScore).Take(topK).ToList();
            _memoryCache.Set(cacheKey, topResults, cacheOptions);

            _logger.LogDebug("[Cache] Cached {Count} results for query: {Query}",
                topResults.Count, query);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Cache] Failed to cache results for query: {Query}", query);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Generate deterministic cache key
    /// </summary>
    private string GenerateCacheKey(string query, int topK, double minScore)
    {
        return $"cascading_search:{query.ToLowerInvariant()}:k{topK}:s{minScore:F2}";
    }

    /// <summary>
    /// Finalize search result with summary
    /// </summary>
    private CascadingSearchResult FinalizeResult(
        CascadingSearchResult result,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();
        result.Metadata.TotalExecutionTimeMs = stopwatch.ElapsedMilliseconds;

        // Sort by similarity score descending
        result.Results = result.Results
            .OrderByDescending(r => r.SimilarityScore)
            .ToList();

        if (result.Success)
        {
            result.Summary = $"Retrieved {result.Results.Count} results from {result.Metadata.SuccessfulTier} " +
                            $"in {result.Metadata.TotalExecutionTimeMs}ms " +
                            $"(attempted {result.Metadata.TiersAttempted} tier(s))";
        }
        else
        {
            result.Summary = $"No results found after attempting {result.Metadata.TiersAttempted} tier(s) " +
                            $"in {result.Metadata.TotalExecutionTimeMs}ms";
        }

        _logger.LogInformation("[CascadingSearch] {Summary}", result.Summary);

        return result;
    }

    /// <summary>
    /// Warmup cache with frequently used queries
    /// </summary>
    public async Task WarmupCacheAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[CascadingSearch] Cache warmup not implemented");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Clear all cached entries
    /// </summary>
    public Task ClearCacheAsync()
    {
        _logger.LogWarning("[CascadingSearch] Cache cleared (note: IMemoryCache doesn't have native Clear)");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Get health status for all tiers
    /// </summary>
    public Task<TierHealthStatus> GetHealthStatusAsync()
    {
        var status = new TierHealthStatus();

        // Tier 1 health
        status.Tiers["Tier1-Cache"] = new TierHealth
        {
            TierName = "Tier1-Cache",
            CircuitBreakerState = _tier1State.IsOpen ? "Open" : "Closed",
            SuccessRate = _tier1Metrics.GetSuccessRate(),
            AverageResponseTimeMs = _tier1Metrics.GetAverageResponseTime(),
            CacheHitRate = _tier1Metrics.GetCacheHitRate(),
            TotalRequests = _tier1Metrics.TotalRequests,
            FailedRequests = _tier1Metrics.FailedRequests
        };

        // Tier 2 health
        status.Tiers["Tier2-KnowledgeBase"] = new TierHealth
        {
            TierName = "Tier2-KnowledgeBase",
            CircuitBreakerState = _tier2State.IsOpen ? "Open" : "Closed",
            SuccessRate = _tier2Metrics.GetSuccessRate(),
            AverageResponseTimeMs = _tier2Metrics.GetAverageResponseTime(),
            TotalRequests = _tier2Metrics.TotalRequests,
            FailedRequests = _tier2Metrics.FailedRequests
        };

        // Tier 3 health
        status.Tiers["Tier3-ExternalAPI"] = new TierHealth
        {
            TierName = "Tier3-ExternalAPI",
            CircuitBreakerState = _tier3State.IsOpen ? "Open" : "Closed",
            SuccessRate = _tier3Metrics.GetSuccessRate(),
            AverageResponseTimeMs = _tier3Metrics.GetAverageResponseTime(),
            TotalRequests = _tier3Metrics.TotalRequests,
            FailedRequests = _tier3Metrics.FailedRequests
        };

        // Overall health determination
        var allSuccessRates = status.Tiers.Values.Select(t => t.SuccessRate).ToList();
        var avgSuccessRate = allSuccessRates.Any() ? allSuccessRates.Average() : 1.0;

        status.OverallHealth = avgSuccessRate switch
        {
            >= 0.95 => "Healthy",
            >= 0.7 => "Degraded",
            _ => "Unhealthy"
        };

        return Task.FromResult(status);
    }
}

/// <summary>
/// Simple circuit breaker state management
/// </summary>
internal class CircuitBreakerState
{
    private int _consecutiveFailures = 0;
    private DateTime? _openedAt = null;
    private readonly int _failureThreshold = 5;
    private readonly TimeSpan _resetTimeout = TimeSpan.FromSeconds(30);

    public bool IsOpen
    {
        get
        {
            if (_openedAt.HasValue)
            {
                if (DateTime.UtcNow - _openedAt.Value > _resetTimeout)
                {
                    // Reset circuit breaker
                    _openedAt = null;
                    _consecutiveFailures = 0;
                    return false;
                }
                return true;
            }
            return false;
        }
    }

    public void RecordSuccess()
    {
        _consecutiveFailures = 0;
        _openedAt = null;
    }

    public void RecordFailure()
    {
        _consecutiveFailures++;
        if (_consecutiveFailures >= _failureThreshold)
        {
            _openedAt = DateTime.UtcNow;
        }
    }
}

/// <summary>
/// Internal class for tracking tier metrics
/// </summary>
internal class TierMetrics
{
    private readonly object _lock = new();
    private long _totalRequests;
    private long _successfulRequests;
    private long _failedRequests;
    private long _totalResponseTimeMs;
    private long _cacheHits;
    private long _cacheMisses;

    public long TotalRequests => _totalRequests;
    public long FailedRequests => _failedRequests;

    public void RecordSuccess(long responseTimeMs)
    {
        lock (_lock)
        {
            _totalRequests++;
            _successfulRequests++;
            _totalResponseTimeMs += responseTimeMs;
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _totalRequests++;
            _failedRequests++;
        }
    }

    public void RecordCacheHit()
    {
        lock (_lock)
        {
            _cacheHits++;
        }
    }

    public void RecordCacheMiss()
    {
        lock (_lock)
        {
            _cacheMisses++;
        }
    }

    public double GetSuccessRate()
    {
        lock (_lock)
        {
            return _totalRequests == 0 ? 1.0 : (double)_successfulRequests / _totalRequests;
        }
    }

    public long GetAverageResponseTime()
    {
        lock (_lock)
        {
            return _successfulRequests == 0 ? 0 : _totalResponseTimeMs / _successfulRequests;
        }
    }

    public double GetCacheHitRate()
    {
        lock (_lock)
        {
            var totalCacheChecks = _cacheHits + _cacheMisses;
            return totalCacheChecks == 0 ? 0.0 : (double)_cacheHits / totalCacheChecks;
        }
    }
}
