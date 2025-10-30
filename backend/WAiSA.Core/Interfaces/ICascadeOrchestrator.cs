using WAiSA.Shared.Models;

namespace WAiSA.Core.Interfaces;

/// <summary>
/// Orchestrates the cascading search across multiple sources:
/// Knowledge Base → LLM Knowledge → Microsoft Docs → Web Search
/// with early stopping based on confidence thresholds and MS Docs tie-breaking.
/// </summary>
public interface ICascadeOrchestrator
{
    /// <summary>
    /// Executes the full cascade search with early stopping and conflict resolution.
    /// </summary>
    /// <param name="request">Cascade request with query and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cascade result with final response and execution details</returns>
    Task<CascadeResult> ExecuteCascadeAsync(
        CascadeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current cascade configuration.
    /// </summary>
    /// <returns>Current cascade configuration</returns>
    CascadeConfiguration GetConfiguration();

    /// <summary>
    /// Updates cascade configuration at runtime.
    /// </summary>
    /// <param name="configuration">New configuration</param>
    void UpdateConfiguration(CascadeConfiguration configuration);

    /// <summary>
    /// Gets health status of all cascade stages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health status for each stage</returns>
    Task<CascadeHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets performance metrics for cascade operations.
    /// </summary>
    /// <returns>Performance metrics</returns>
    Task<CascadeMetrics> GetMetricsAsync();
}

/// <summary>
/// Request for cascade search operation.
/// </summary>
public class CascadeRequest
{
    /// <summary>
    /// User query to search for
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Device ID for context and personalization
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Cascade configuration (uses default if null)
    /// </summary>
    public CascadeConfiguration? Configuration { get; set; }

    /// <summary>
    /// Additional context for the query (conversation history, user preferences, etc.)
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Maximum total execution time in seconds
    /// </summary>
    public int? MaxExecutionTimeSeconds { get; set; }
}

/// <summary>
/// Configuration for cascade search behavior.
/// </summary>
public class CascadeConfiguration
{
    /// <summary>
    /// Confidence threshold for Knowledge Base (0.0-1.0, default 0.85)
    /// </summary>
    public double KnowledgeBaseThreshold { get; set; } = 0.85;

    /// <summary>
    /// Confidence threshold for LLM (0.0-1.0, default 0.75)
    /// </summary>
    public double LLMThreshold { get; set; } = 0.75;

    /// <summary>
    /// Confidence threshold for Microsoft Docs (0.0-1.0, default 0.80)
    /// </summary>
    public double MicrosoftDocsThreshold { get; set; } = 0.80;

    /// <summary>
    /// Confidence threshold for Web Search (0.0-1.0, default 0.70)
    /// </summary>
    public double WebSearchThreshold { get; set; } = 0.70;

    /// <summary>
    /// Enable early stopping when threshold is met (default true)
    /// </summary>
    public bool EnableEarlyStopping { get; set; } = true;

    /// <summary>
    /// Use Microsoft Docs as tie-breaker for conflicts (default true)
    /// </summary>
    public bool EnableMSDocsTieBreaker { get; set; } = true;

    /// <summary>
    /// Enable conflict detection between sources (default true)
    /// </summary>
    public bool EnableConflictDetection { get; set; } = true;

    /// <summary>
    /// Maximum results to retrieve per stage
    /// </summary>
    public int MaxResultsPerStage { get; set; } = 5;

    /// <summary>
    /// Timeout for each stage in seconds
    /// </summary>
    public int StageTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum total cascade execution time in seconds
    /// </summary>
    public int MaxTotalTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Enable parallel execution of non-dependent stages
    /// </summary>
    public bool EnableParallelExecution { get; set; } = false;
}

/// <summary>
/// Result from cascade search operation.
/// </summary>
public class CascadeResult
{
    /// <summary>
    /// Final response text (possibly merged or resolved from multiple sources)
    /// </summary>
    public string FinalResponse { get; set; } = string.Empty;

    /// <summary>
    /// Stage at which cascade stopped (due to threshold or completion)
    /// </summary>
    public CascadeStage StoppedAtStage { get; set; }

    /// <summary>
    /// Final confidence score for the response
    /// </summary>
    public ConfidenceScore FinalConfidence { get; set; } = new();

    /// <summary>
    /// Detailed execution information for each stage
    /// </summary>
    public List<StageExecution> StageExecutions { get; set; } = new();

    /// <summary>
    /// Conflict analysis if conflicts were detected
    /// </summary>
    public ConflictAnalysis? ConflictAnalysis { get; set; }

    /// <summary>
    /// Total execution time for entire cascade in milliseconds
    /// </summary>
    public long TotalExecutionTimeMs { get; set; }

    /// <summary>
    /// Whether early stopping was triggered
    /// </summary>
    public bool EarlyStoppingStopped { get; set; }

    /// <summary>
    /// Additional metadata about the cascade execution
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Stages in the cascade search.
/// </summary>
public enum CascadeStage
{
    /// <summary>
    /// Knowledge Base search
    /// </summary>
    KnowledgeBase,

    /// <summary>
    /// LLM knowledge query
    /// </summary>
    LLM,

    /// <summary>
    /// Microsoft Docs search
    /// </summary>
    MicrosoftDocs,

    /// <summary>
    /// Web search
    /// </summary>
    WebSearch,

    /// <summary>
    /// Cascade completed without early stopping
    /// </summary>
    Completed,

    /// <summary>
    /// Cascade failed or timed out
    /// </summary>
    Failed
}

/// <summary>
/// Execution details for a single stage in the cascade.
/// </summary>
public class StageExecution
{
    /// <summary>
    /// Stage identifier
    /// </summary>
    public CascadeStage Stage { get; set; }

    /// <summary>
    /// Whether this stage was executed
    /// </summary>
    public bool Executed { get; set; }

    /// <summary>
    /// Response from this stage
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score for this stage
    /// </summary>
    public ConfidenceScore? ConfidenceScore { get; set; }

    /// <summary>
    /// Execution time for this stage in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Whether this stage triggered early stopping
    /// </summary>
    public bool TriggeredEarlyStopping { get; set; }

    /// <summary>
    /// Error message if stage failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of results retrieved
    /// </summary>
    public int ResultCount { get; set; }

    /// <summary>
    /// Start timestamp
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End timestamp
    /// </summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// Health status for cascade operations.
/// </summary>
public class CascadeHealthStatus
{
    /// <summary>
    /// Overall health (Healthy, Degraded, Unhealthy)
    /// </summary>
    public HealthStatus OverallStatus { get; set; }

    /// <summary>
    /// Health status for each stage
    /// </summary>
    public Dictionary<CascadeStage, StageHealthStatus> StageStatuses { get; set; } = new();

    /// <summary>
    /// Last health check timestamp
    /// </summary>
    public DateTime LastCheckTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Health status for a single cascade stage.
/// </summary>
public class StageHealthStatus
{
    /// <summary>
    /// Stage identifier
    /// </summary>
    public CascadeStage Stage { get; set; }

    /// <summary>
    /// Health status
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double AverageResponseTimeMs { get; set; }

    /// <summary>
    /// Success rate (0.0-1.0)
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Number of recent failures
    /// </summary>
    public int RecentFailures { get; set; }

    /// <summary>
    /// Additional status details
    /// </summary>
    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// Overall health status.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// All systems operational
    /// </summary>
    Healthy,

    /// <summary>
    /// Some degradation but still functional
    /// </summary>
    Degraded,

    /// <summary>
    /// System not functioning properly
    /// </summary>
    Unhealthy
}

/// <summary>
/// Performance metrics for cascade operations.
/// </summary>
public class CascadeMetrics
{
    /// <summary>
    /// Total number of cascade operations executed
    /// </summary>
    public long TotalOperations { get; set; }

    /// <summary>
    /// Number of operations that stopped at each stage
    /// </summary>
    public Dictionary<CascadeStage, long> StoppedAtStage { get; set; } = new();

    /// <summary>
    /// Average execution time per stage in milliseconds
    /// </summary>
    public Dictionary<CascadeStage, double> AverageExecutionTimeMs { get; set; } = new();

    /// <summary>
    /// Average confidence score per stage
    /// </summary>
    public Dictionary<CascadeStage, double> AverageConfidence { get; set; } = new();

    /// <summary>
    /// Number of conflicts detected
    /// </summary>
    public long ConflictsDetected { get; set; }

    /// <summary>
    /// Number of times MS Docs was used as tie-breaker
    /// </summary>
    public long MSDocsTieBreakerUsed { get; set; }

    /// <summary>
    /// Overall success rate (0.0-1.0)
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Metrics collection period
    /// </summary>
    public TimeSpan CollectionPeriod { get; set; }
}
