using System.Collections.Concurrent;
using WAiSA.API.Security.Models;

namespace WAiSA.API.Security.Filtering;

/// <summary>
/// Token bucket rate limiter for agent command execution
/// </summary>
public sealed class RateLimiter : IRateLimiter
{
    private readonly ILogger<RateLimiter> _logger;
    private readonly ConcurrentDictionary<string, TokenBucket> _agentBuckets;
    private readonly ConcurrentDictionary<string, SessionLimits> _sessionLimits;

    // Configuration (should be loaded from config)
    private readonly int _bucketCapacity = 100;
    private readonly int _refillRatePerSecond = 10;
    private readonly int _burstAllowance = 20;
    private readonly int _requestsPerMinute = 100;
    private readonly int _requestsPerHour = 1000;

    public RateLimiter(ILogger<RateLimiter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentBuckets = new ConcurrentDictionary<string, TokenBucket>();
        _sessionLimits = new ConcurrentDictionary<string, SessionLimits>();

        // Start background refill task
        _ = Task.Run(RefillBucketsAsync);
    }

    /// <summary>
    /// Check if agent is within rate limits
    /// </summary>
    public async Task<RateLimitResult> CheckRateLimitAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await Task.CompletedTask; // Placeholder for async operations

        // Get or create token bucket for agent
        var bucket = _agentBuckets.GetOrAdd(context.AgentId, _ => new TokenBucket
        {
            AgentId = context.AgentId,
            Capacity = _bucketCapacity,
            Tokens = _bucketCapacity,
            LastRefill = DateTime.UtcNow
        });

        // Get or create session limits
        var sessionLimits = _sessionLimits.GetOrAdd(context.SessionId, _ => new SessionLimits
        {
            SessionId = context.SessionId,
            WindowStart = DateTime.UtcNow
        });

        // Check token bucket (burst protection)
        lock (bucket)
        {
            if (bucket.Tokens < 1)
            {
                var retryAfter = CalculateRetryAfter(bucket);
                _logger.LogWarning(
                    "Token bucket depleted for AgentId={AgentId}. RetryAfter={RetryAfter}",
                    context.AgentId,
                    retryAfter);

                return new RateLimitResult(false, 0, retryAfter);
            }

            bucket.Tokens--;
            bucket.LastRequest = DateTime.UtcNow;
        }

        // Check per-minute limits
        lock (sessionLimits)
        {
            CleanupOldRequests(sessionLimits);

            var recentRequests = sessionLimits.RequestsPerMinute.Count(r =>
                r > DateTime.UtcNow.AddMinutes(-1));

            if (recentRequests >= _requestsPerMinute)
            {
                _logger.LogWarning(
                    "Per-minute rate limit exceeded for SessionId={SessionId}. Requests={Requests}",
                    context.SessionId,
                    recentRequests);

                return new RateLimitResult(false, 0, TimeSpan.FromMinutes(1));
            }

            // Check per-hour limits
            var hourlyRequests = sessionLimits.RequestsPerHour.Count(r =>
                r > DateTime.UtcNow.AddHours(-1));

            if (hourlyRequests >= _requestsPerHour)
            {
                _logger.LogWarning(
                    "Per-hour rate limit exceeded for SessionId={SessionId}. Requests={Requests}",
                    context.SessionId,
                    hourlyRequests);

                return new RateLimitResult(false, 0, TimeSpan.FromHours(1));
            }

            // Record this request
            sessionLimits.RequestsPerMinute.Add(DateTime.UtcNow);
            sessionLimits.RequestsPerHour.Add(DateTime.UtcNow);

            var remaining = _requestsPerMinute - recentRequests - 1;

            _logger.LogDebug(
                "Rate limit check passed for AgentId={AgentId}, Remaining={Remaining}",
                context.AgentId,
                remaining);

            return new RateLimitResult(true, remaining);
        }
    }

    /// <summary>
    /// Background task to refill token buckets
    /// </summary>
    private async Task RefillBucketsAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));

            foreach (var bucket in _agentBuckets.Values)
            {
                lock (bucket)
                {
                    var elapsed = (DateTime.UtcNow - bucket.LastRefill).TotalSeconds;
                    var tokensToAdd = (int)(elapsed * _refillRatePerSecond);

                    if (tokensToAdd > 0)
                    {
                        bucket.Tokens = Math.Min(bucket.Capacity + _burstAllowance, bucket.Tokens + tokensToAdd);
                        bucket.LastRefill = DateTime.UtcNow;
                    }
                }
            }

            // Cleanup old session limits (older than 1 hour)
            var oldSessions = _sessionLimits
                .Where(kvp => (DateTime.UtcNow - kvp.Value.WindowStart).TotalHours > 1)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in oldSessions)
            {
                _sessionLimits.TryRemove(sessionId, out _);
            }
        }
    }

    private static void CleanupOldRequests(SessionLimits limits)
    {
        var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);

        limits.RequestsPerMinute.RemoveAll(r => r < oneMinuteAgo);
        limits.RequestsPerHour.RemoveAll(r => r < oneHourAgo);
    }

    private TimeSpan CalculateRetryAfter(TokenBucket bucket)
    {
        var tokensNeeded = 1 - bucket.Tokens;
        var secondsToWait = tokensNeeded / _refillRatePerSecond;
        return TimeSpan.FromSeconds(Math.Max(1, secondsToWait));
    }

    private sealed class TokenBucket
    {
        public required string AgentId { get; init; }
        public required int Capacity { get; init; }
        public double Tokens { get; set; }
        public DateTime LastRefill { get; set; }
        public DateTime LastRequest { get; set; }
    }

    private sealed class SessionLimits
    {
        public required string SessionId { get; init; }
        public DateTime WindowStart { get; init; }
        public List<DateTime> RequestsPerMinute { get; } = new();
        public List<DateTime> RequestsPerHour { get; } = new();
    }
}
