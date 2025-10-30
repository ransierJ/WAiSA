using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using WAiSA.Core.Interfaces;
using WAiSA.Shared.Models;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// Orchestrates the cascading search across KB → LLM → MS Docs → Web Search
/// with early stopping and conflict resolution.
/// </summary>
public class CascadeOrchestrator : ICascadeOrchestrator
{
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly IMicrosoftDocsService _microsoftDocsService;
    private readonly IWebSearchService _webSearchService;
    private readonly IConfidenceScorer _confidenceScorer;
    private readonly IAIOrchestrationService _aiService;
    private readonly ILogger<CascadeOrchestrator> _logger;
    private CascadeConfiguration _configuration;

    // Metrics tracking
    private long _totalOperations = 0;
    private readonly Dictionary<CascadeStage, long> _stoppedAtStage = new();
    private readonly Dictionary<CascadeStage, List<long>> _executionTimes = new();
    private long _conflictsDetected = 0;
    private long _msDocsTieBreakerUsed = 0;

    public CascadeOrchestrator(
        IKnowledgeBaseService knowledgeBaseService,
        IMicrosoftDocsService microsoftDocsService,
        IWebSearchService webSearchService,
        IConfidenceScorer confidenceScorer,
        IAIOrchestrationService aiService,
        IConfiguration configuration,
        ILogger<CascadeOrchestrator> logger)
    {
        _knowledgeBaseService = knowledgeBaseService ?? throw new ArgumentNullException(nameof(knowledgeBaseService));
        _microsoftDocsService = microsoftDocsService ?? throw new ArgumentNullException(nameof(microsoftDocsService));
        _webSearchService = webSearchService ?? throw new ArgumentNullException(nameof(webSearchService));
        _confidenceScorer = confidenceScorer ?? throw new ArgumentNullException(nameof(confidenceScorer));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _configuration = BuildConfiguration(configuration);
        InitializeMetrics();
    }

    public async Task<CascadeResult> ExecuteCascadeAsync(
        CascadeRequest request,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var config = request.Configuration ?? _configuration;
        var stageExecutions = new List<StageExecution>();
        var responses = new List<(SourceType SourceType, string Response)>();

        _logger.LogInformation("Starting cascade for query: {Query}", request.Query);
        _totalOperations++;

        try
        {
            // STAGE 1: Knowledge Base
            var (kbExecution, kbShouldStop) = await ExecuteKnowledgeBaseStageAsync(
                request.Query, config, cancellationToken);
            stageExecutions.Add(kbExecution);

            if (!string.IsNullOrEmpty(kbExecution.Response))
            {
                responses.Add((SourceType.KnowledgeBase, kbExecution.Response));
            }

            if (kbShouldStop && config.EnableEarlyStopping)
            {
                totalStopwatch.Stop();
                return BuildResult(request.Query, responses, stageExecutions, config,
                    CascadeStage.KnowledgeBase, totalStopwatch.ElapsedMilliseconds, earlyStop: true);
            }

            // STAGE 2: LLM Knowledge
            var (llmExecution, llmShouldStop) = await ExecuteLLMStageAsync(
                request.Query, request.DeviceId, request.Context, config, cancellationToken);
            stageExecutions.Add(llmExecution);

            if (!string.IsNullOrEmpty(llmExecution.Response))
            {
                responses.Add((SourceType.LLM, llmExecution.Response));
            }

            if (llmShouldStop && config.EnableEarlyStopping)
            {
                totalStopwatch.Stop();
                return BuildResult(request.Query, responses, stageExecutions, config,
                    CascadeStage.LLM, totalStopwatch.ElapsedMilliseconds, earlyStop: true);
            }

            // STAGE 3: Microsoft Docs
            var (msDocsExecution, msDocsShouldStop) = await ExecuteMicrosoftDocsStageAsync(
                request.Query, config, cancellationToken);
            stageExecutions.Add(msDocsExecution);

            if (!string.IsNullOrEmpty(msDocsExecution.Response))
            {
                responses.Add((SourceType.MicrosoftDocs, msDocsExecution.Response));
            }

            if (msDocsShouldStop && config.EnableEarlyStopping)
            {
                totalStopwatch.Stop();
                return BuildResult(request.Query, responses, stageExecutions, config,
                    CascadeStage.MicrosoftDocs, totalStopwatch.ElapsedMilliseconds, earlyStop: true);
            }

            // STAGE 4: Web Search (final fallback)
            var (webExecution, _) = await ExecuteWebSearchStageAsync(
                request.Query, config, cancellationToken);
            stageExecutions.Add(webExecution);

            if (!string.IsNullOrEmpty(webExecution.Response))
            {
                responses.Add((SourceType.WebSearch, webExecution.Response));
            }

            totalStopwatch.Stop();
            return BuildResult(request.Query, responses, stageExecutions, config,
                CascadeStage.Completed, totalStopwatch.ElapsedMilliseconds, earlyStop: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cascade execution for query: {Query}", request.Query);
            totalStopwatch.Stop();

            return new CascadeResult
            {
                FinalResponse = "An error occurred during the cascade search process.",
                StoppedAtStage = CascadeStage.Failed,
                StageExecutions = stageExecutions,
                TotalExecutionTimeMs = totalStopwatch.ElapsedMilliseconds,
                Metadata = new Dictionary<string, object>
                {
                    { "Error", ex.Message },
                    { "ErrorType", ex.GetType().Name }
                }
            };
        }
    }

    private async Task<(StageExecution Execution, bool ShouldStop)> ExecuteKnowledgeBaseStageAsync(
        string query,
        CascadeConfiguration config,
        CancellationToken cancellationToken)
    {
        var execution = new StageExecution
        {
            Stage = CascadeStage.KnowledgeBase,
            Executed = true,
            StartTime = DateTime.UtcNow
        };

        var stageStopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Executing Knowledge Base stage");

            var kbResults = await _knowledgeBaseService.RetrieveRelevantKnowledgeAsync(
                query,
                topK: config.MaxResultsPerStage,
                minScore: 0.7,
                cancellationToken);

            execution.ResultCount = kbResults?.Count ?? 0;

            if (kbResults?.Any() == true)
            {
                // Build response from KB results
                execution.Response = string.Join("\n\n", kbResults.Select(r =>
                    $"[KB - Score: {r.SimilarityScore:F2}] {r.Entry.Content}"));

                // Score the KB results
                execution.ConfidenceScore = _confidenceScorer.ScoreKnowledgeBase(query, kbResults);

                _logger.LogInformation("KB stage: {Count} results, confidence {Score:F2}",
                    kbResults.Count, execution.ConfidenceScore.Score);
            }
            else
            {
                execution.ConfidenceScore = new ConfidenceScore
                {
                    Source = SourceType.KnowledgeBase,
                    Score = 0.0,
                    Reasoning = "No KB results",
                    MeetsThreshold = false,
                    Threshold = config.KnowledgeBaseThreshold
                };
            }

            stageStopwatch.Stop();
            execution.ExecutionTimeMs = stageStopwatch.ElapsedMilliseconds;
            execution.EndTime = DateTime.UtcNow;
            execution.TriggeredEarlyStopping = execution.ConfidenceScore.MeetsThreshold;

            TrackStageMetrics(CascadeStage.KnowledgeBase, execution.ExecutionTimeMs);

            return (execution, execution.TriggeredEarlyStopping);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Knowledge Base stage");
            stageStopwatch.Stop();
            execution.ExecutionTimeMs = stageStopwatch.ElapsedMilliseconds;
            execution.EndTime = DateTime.UtcNow;
            execution.ErrorMessage = ex.Message;
            return (execution, false);
        }
    }

    private async Task<(StageExecution Execution, bool ShouldStop)> ExecuteLLMStageAsync(
        string query,
        string deviceId,
        Dictionary<string, object> context,
        CascadeConfiguration config,
        CancellationToken cancellationToken)
    {
        var execution = new StageExecution
        {
            Stage = CascadeStage.LLM,
            Executed = true,
            StartTime = DateTime.UtcNow
        };

        var stageStopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Executing LLM stage");

            // Extract recent interactions from context if available
            List<Interaction>? recentInteractions = null;
            if (context.ContainsKey("recentInteractions") && context["recentInteractions"] is List<Interaction> interactions)
            {
                recentInteractions = interactions;
                _logger.LogInformation("Found {Count} recent interactions in context for LLM stage", recentInteractions.Count);
            }

            // Call LLM with the query and conversation history
            var llmResponse = await _aiService.ProcessMessageAsync(deviceId, query, recentInteractions, cancellationToken);

            execution.Response = llmResponse.Message;
            execution.ResultCount = 1;

            // Score the LLM response
            var metadata = new Dictionary<string, object>
            {
                { "response", llmResponse.Message },
                { "context", context }
            };

            execution.ConfidenceScore = await _confidenceScorer.ScoreResponseAsync(
                query, llmResponse.Message, SourceType.LLM, metadata);

            _logger.LogInformation("LLM stage: confidence {Score:F2}",
                execution.ConfidenceScore.Score);

            stageStopwatch.Stop();
            execution.ExecutionTimeMs = stageStopwatch.ElapsedMilliseconds;
            execution.EndTime = DateTime.UtcNow;
            execution.TriggeredEarlyStopping = execution.ConfidenceScore.MeetsThreshold;

            TrackStageMetrics(CascadeStage.LLM, execution.ExecutionTimeMs);

            return (execution, execution.TriggeredEarlyStopping);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM stage");
            stageStopwatch.Stop();
            execution.ExecutionTimeMs = stageStopwatch.ElapsedMilliseconds;
            execution.EndTime = DateTime.UtcNow;
            execution.ErrorMessage = ex.Message;
            return (execution, false);
        }
    }

    private async Task<(StageExecution Execution, bool ShouldStop)> ExecuteMicrosoftDocsStageAsync(
        string query,
        CascadeConfiguration config,
        CancellationToken cancellationToken)
    {
        var execution = new StageExecution
        {
            Stage = CascadeStage.MicrosoftDocs,
            Executed = true,
            StartTime = DateTime.UtcNow
        };

        var stageStopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Executing Microsoft Docs stage");

            var msDocsResults = await _microsoftDocsService.SearchAsync(
                query, config.MaxResultsPerStage, cancellationToken);

            execution.ResultCount = msDocsResults.Results.Count;

            if (msDocsResults.Results.Any())
            {
                // Build response from MS Docs results
                execution.Response = string.Join("\n\n", msDocsResults.Results.Select(r =>
                    $"[MS Docs - {r.Title}]\n{r.Snippet}\nURL: {r.Url}"));

                // Score the MS Docs results
                execution.ConfidenceScore = _confidenceScorer.ScoreMicrosoftDocs(query, msDocsResults);

                _logger.LogInformation("MS Docs stage: {Count} results, confidence {Score:F2}",
                    msDocsResults.Results.Count, execution.ConfidenceScore.Score);
            }
            else
            {
                execution.ConfidenceScore = new ConfidenceScore
                {
                    Source = SourceType.MicrosoftDocs,
                    Score = 0.0,
                    Reasoning = "No MS Docs results",
                    MeetsThreshold = false,
                    Threshold = config.MicrosoftDocsThreshold
                };
            }

            stageStopwatch.Stop();
            execution.ExecutionTimeMs = stageStopwatch.ElapsedMilliseconds;
            execution.EndTime = DateTime.UtcNow;
            execution.TriggeredEarlyStopping = execution.ConfidenceScore.MeetsThreshold;

            TrackStageMetrics(CascadeStage.MicrosoftDocs, execution.ExecutionTimeMs);

            return (execution, execution.TriggeredEarlyStopping);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Microsoft Docs stage");
            stageStopwatch.Stop();
            execution.ExecutionTimeMs = stageStopwatch.ElapsedMilliseconds;
            execution.EndTime = DateTime.UtcNow;
            execution.ErrorMessage = ex.Message;
            return (execution, false);
        }
    }

    private async Task<(StageExecution Execution, bool ShouldStop)> ExecuteWebSearchStageAsync(
        string query,
        CascadeConfiguration config,
        CancellationToken cancellationToken)
    {
        var execution = new StageExecution
        {
            Stage = CascadeStage.WebSearch,
            Executed = true,
            StartTime = DateTime.UtcNow
        };

        var stageStopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Executing Web Search stage (final fallback)");

            var webResults = await _webSearchService.SearchAsync(
                query, config.MaxResultsPerStage, cancellationToken);

            execution.ResultCount = webResults.Results.Count;

            if (webResults.Results.Any())
            {
                // Build response from web results
                execution.Response = string.Join("\n\n", webResults.Results.Select(r =>
                    $"[Web - {r.Title}]\n{r.Snippet}\nDomain: {r.Domain}\nURL: {r.Url}"));

                // Score the web results
                execution.ConfidenceScore = _confidenceScorer.ScoreWebSearch(query, webResults);

                _logger.LogInformation("Web stage: {Count} results, confidence {Score:F2}",
                    webResults.Results.Count, execution.ConfidenceScore.Score);
            }
            else
            {
                execution.ConfidenceScore = new ConfidenceScore
                {
                    Source = SourceType.WebSearch,
                    Score = 0.0,
                    Reasoning = "No web results",
                    MeetsThreshold = false,
                    Threshold = config.WebSearchThreshold
                };
            }

            stageStopwatch.Stop();
            execution.ExecutionTimeMs = stageStopwatch.ElapsedMilliseconds;
            execution.EndTime = DateTime.UtcNow;
            execution.TriggeredEarlyStopping = false; // Web search is final stage, never triggers early stop

            TrackStageMetrics(CascadeStage.WebSearch, execution.ExecutionTimeMs);

            return (execution, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Web Search stage");
            stageStopwatch.Stop();
            execution.ExecutionTimeMs = stageStopwatch.ElapsedMilliseconds;
            execution.EndTime = DateTime.UtcNow;
            execution.ErrorMessage = ex.Message;
            return (execution, false);
        }
    }

    private CascadeResult BuildResult(
        string query,
        List<(SourceType SourceType, string Response)> responses,
        List<StageExecution> stageExecutions,
        CascadeConfiguration config,
        CascadeStage stoppedAtStage,
        long totalExecutionTimeMs,
        bool earlyStop)
    {
        ConflictAnalysis? conflictAnalysis = null;
        string finalResponse;
        ConfidenceScore finalConfidence;

        // Detect conflicts if enabled and we have multiple responses
        if (config.EnableConflictDetection && responses.Count > 1)
        {
            conflictAnalysis = _confidenceScorer.DetectConflictsAsync(responses).Result;

            if (conflictAnalysis.HasConflicts)
            {
                _conflictsDetected++;
                _logger.LogWarning("Conflicts detected between sources: {Sources}",
                    string.Join(", ", conflictAnalysis.ConflictingSources));

                // Use MS Docs as tie-breaker if available
                var msDocsResponse = responses.FirstOrDefault(r => r.SourceType == SourceType.MicrosoftDocs);
                if (config.EnableMSDocsTieBreaker && !string.IsNullOrEmpty(msDocsResponse.Response))
                {
                    _msDocsTieBreakerUsed++;
                    _logger.LogInformation("Using Microsoft Docs as tie-breaker");
                    finalResponse = _confidenceScorer.ResolveConflict(conflictAnalysis, msDocsResponse);

                    var msDocsExecution = stageExecutions.FirstOrDefault(e => e.Stage == CascadeStage.MicrosoftDocs);
                    finalConfidence = msDocsExecution?.ConfidenceScore ?? new ConfidenceScore
                    {
                        Source = SourceType.MicrosoftDocs,
                        Score = 0.8,
                        Reasoning = "MS Docs tie-breaker"
                    };
                }
                else
                {
                    // Use highest confidence response
                    var bestExecution = stageExecutions
                        .Where(e => e.ConfidenceScore != null)
                        .OrderByDescending(e => e.ConfidenceScore!.Score)
                        .FirstOrDefault();

                    finalResponse = bestExecution?.Response ?? responses.Last().Response;
                    finalConfidence = bestExecution?.ConfidenceScore ?? new ConfidenceScore
                    {
                        Score = 0.5,
                        Reasoning = "Fallback to best available response"
                    };
                }
            }
            else
            {
                // No conflicts, use the response from stopped stage
                var stoppedExecution = stageExecutions.FirstOrDefault(e => e.Stage == stoppedAtStage)
                                     ?? stageExecutions.Last();
                finalResponse = stoppedExecution.Response;
                finalConfidence = stoppedExecution.ConfidenceScore ?? new ConfidenceScore
                {
                    Score = 0.5,
                    Reasoning = "No conflicts detected"
                };
            }
        }
        else
        {
            // Use the response from the stopped stage
            var stoppedExecution = stageExecutions.FirstOrDefault(e => e.Stage == stoppedAtStage)
                                 ?? stageExecutions.LastOrDefault(e => !string.IsNullOrEmpty(e.Response))
                                 ?? stageExecutions.Last();

            finalResponse = stoppedExecution.Response;
            finalConfidence = stoppedExecution.ConfidenceScore ?? new ConfidenceScore
            {
                Score = 0.5,
                Reasoning = "Single source result"
            };
        }

        // Track stopped stage
        if (!_stoppedAtStage.ContainsKey(stoppedAtStage))
            _stoppedAtStage[stoppedAtStage] = 0;
        _stoppedAtStage[stoppedAtStage]++;

        return new CascadeResult
        {
            FinalResponse = finalResponse,
            StoppedAtStage = stoppedAtStage,
            FinalConfidence = finalConfidence,
            StageExecutions = stageExecutions,
            ConflictAnalysis = conflictAnalysis,
            TotalExecutionTimeMs = totalExecutionTimeMs,
            EarlyStoppingStopped = earlyStop,
            Metadata = new Dictionary<string, object>
            {
                { "Query", query },
                { "StagesExecuted", stageExecutions.Count },
                { "ResponseCount", responses.Count },
                { "ConflictsDetected", conflictAnalysis?.HasConflicts ?? false }
            }
        };
    }

    public CascadeConfiguration GetConfiguration()
    {
        return _configuration;
    }

    public void UpdateConfiguration(CascadeConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger.LogInformation("Cascade configuration updated");
    }

    public async Task<CascadeHealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        var stageStatuses = new Dictionary<CascadeStage, StageHealthStatus>();

        // Check each stage
        try
        {
            var kbHealthy = true; // Assume KB is healthy (no direct health check in interface)
            stageStatuses[CascadeStage.KnowledgeBase] = new StageHealthStatus
            {
                Stage = CascadeStage.KnowledgeBase,
                Status = kbHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                AverageResponseTimeMs = GetAverageExecutionTime(CascadeStage.KnowledgeBase),
                SuccessRate = 0.95, // Placeholder
                Details = "Knowledge Base operational"
            };

            var msDocsHealthy = await _microsoftDocsService.HealthCheckAsync(cancellationToken);
            stageStatuses[CascadeStage.MicrosoftDocs] = new StageHealthStatus
            {
                Stage = CascadeStage.MicrosoftDocs,
                Status = msDocsHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                AverageResponseTimeMs = GetAverageExecutionTime(CascadeStage.MicrosoftDocs),
                SuccessRate = msDocsHealthy ? 0.98 : 0.0,
                Details = msDocsHealthy ? "MS Docs service operational" : "MS Docs service unavailable"
            };

            var webHealthy = await _webSearchService.HealthCheckAsync(cancellationToken);
            stageStatuses[CascadeStage.WebSearch] = new StageHealthStatus
            {
                Stage = CascadeStage.WebSearch,
                Status = webHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                AverageResponseTimeMs = GetAverageExecutionTime(CascadeStage.WebSearch),
                SuccessRate = webHealthy ? 0.95 : 0.0,
                Details = webHealthy ? "Web search operational" : "Web search unavailable"
            };

            var overallHealthy = stageStatuses.Values.All(s => s.Status == HealthStatus.Healthy);
            var overallStatus = overallHealthy ? HealthStatus.Healthy :
                              stageStatuses.Values.Any(s => s.Status == HealthStatus.Healthy) ? HealthStatus.Degraded :
                              HealthStatus.Unhealthy;

            return new CascadeHealthStatus
            {
                OverallStatus = overallStatus,
                StageStatuses = stageStatuses,
                LastCheckTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cascade health");
            return new CascadeHealthStatus
            {
                OverallStatus = HealthStatus.Unhealthy,
                StageStatuses = stageStatuses,
                LastCheckTime = DateTime.UtcNow
            };
        }
    }

    public Task<CascadeMetrics> GetMetricsAsync()
    {
        var avgExecutionTimes = new Dictionary<CascadeStage, double>();
        foreach (var stage in _executionTimes.Keys)
        {
            avgExecutionTimes[stage] = GetAverageExecutionTime(stage);
        }

        return Task.FromResult(new CascadeMetrics
        {
            TotalOperations = _totalOperations,
            StoppedAtStage = new Dictionary<CascadeStage, long>(_stoppedAtStage),
            AverageExecutionTimeMs = avgExecutionTimes,
            AverageConfidence = new Dictionary<CascadeStage, double>(), // Placeholder
            ConflictsDetected = _conflictsDetected,
            MSDocsTieBreakerUsed = _msDocsTieBreakerUsed,
            SuccessRate = _totalOperations > 0 ? 0.95 : 0.0, // Placeholder
            CollectionPeriod = TimeSpan.FromHours(1) // Placeholder
        });
    }

    private CascadeConfiguration BuildConfiguration(IConfiguration configuration)
    {
        return new CascadeConfiguration
        {
            KnowledgeBaseThreshold = double.TryParse(configuration["Cascade:KnowledgeBaseThreshold"], out var kbThreshold) ? kbThreshold : 0.85,
            LLMThreshold = double.TryParse(configuration["Cascade:LLMThreshold"], out var llmThreshold) ? llmThreshold : 0.75,
            MicrosoftDocsThreshold = double.TryParse(configuration["Cascade:MicrosoftDocsThreshold"], out var docsThreshold) ? docsThreshold : 0.80,
            WebSearchThreshold = double.TryParse(configuration["Cascade:WebSearchThreshold"], out var webThreshold) ? webThreshold : 0.70,
            EnableEarlyStopping = bool.TryParse(configuration["Cascade:EnableEarlyStopping"], out var earlyStop) ? earlyStop : true,
            EnableMSDocsTieBreaker = bool.TryParse(configuration["Cascade:EnableMSDocsTieBreaker"], out var tieBreaker) ? tieBreaker : true,
            EnableConflictDetection = bool.TryParse(configuration["Cascade:EnableConflictDetection"], out var conflictDetect) ? conflictDetect : true,
            MaxResultsPerStage = int.TryParse(configuration["Cascade:MaxResultsPerStage"], out var maxResults) ? maxResults : 5,
            StageTimeoutSeconds = int.TryParse(configuration["Cascade:StageTimeoutSeconds"], out var stageTimeout) ? stageTimeout : 30,
            MaxTotalTimeoutSeconds = int.TryParse(configuration["Cascade:MaxTotalTimeoutSeconds"], out var totalTimeout) ? totalTimeout : 120
        };
    }

    private void InitializeMetrics()
    {
        foreach (CascadeStage stage in Enum.GetValues(typeof(CascadeStage)))
        {
            _executionTimes[stage] = new List<long>();
            _stoppedAtStage[stage] = 0;
        }
    }

    private void TrackStageMetrics(CascadeStage stage, long executionTimeMs)
    {
        if (!_executionTimes.ContainsKey(stage))
            _executionTimes[stage] = new List<long>();

        _executionTimes[stage].Add(executionTimeMs);

        // Keep only last 100 measurements
        if (_executionTimes[stage].Count > 100)
            _executionTimes[stage].RemoveAt(0);
    }

    private double GetAverageExecutionTime(CascadeStage stage)
    {
        if (!_executionTimes.ContainsKey(stage) || !_executionTimes[stage].Any())
            return 0.0;

        return _executionTimes[stage].Average();
    }
}
