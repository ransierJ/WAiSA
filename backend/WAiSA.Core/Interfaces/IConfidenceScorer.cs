using WAiSA.Shared.Models;

namespace WAiSA.Core.Interfaces;

/// <summary>
/// Service for scoring confidence levels of responses from different sources
/// and detecting conflicts between sources.
/// </summary>
public interface IConfidenceScorer
{
    /// <summary>
    /// Scores a response from any source type with confidence level (0.0-1.0).
    /// Uses different scoring algorithms based on source type.
    /// </summary>
    /// <param name="query">Original user query</param>
    /// <param name="response">Response text to score</param>
    /// <param name="sourceType">Type of source (KB, LLM, MSDocs, WebSearch)</param>
    /// <param name="metadata">Additional metadata for scoring (e.g., search scores, result count)</param>
    /// <returns>Confidence score with reasoning</returns>
    Task<ConfidenceScore> ScoreResponseAsync(
        string query,
        string response,
        SourceType sourceType,
        Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Scores Knowledge Base retrieval results based on semantic similarity scores
    /// and result count.
    /// </summary>
    /// <param name="query">Original query</param>
    /// <param name="knowledgeResults">Results from KB search</param>
    /// <returns>Confidence score for KB results</returns>
    ConfidenceScore ScoreKnowledgeBase(string query, List<KnowledgeSearchResult> knowledgeResults);

    /// <summary>
    /// Scores Microsoft Docs search results based on result quality,
    /// recency, and relevance.
    /// </summary>
    /// <param name="query">Original query</param>
    /// <param name="docsResults">Results from MS Docs search</param>
    /// <returns>Confidence score for MS Docs results</returns>
    ConfidenceScore ScoreMicrosoftDocs(string query, MicrosoftDocsSearchResponse docsResults);

    /// <summary>
    /// Scores web search results based on domain authority,
    /// relevance, and result diversity.
    /// </summary>
    /// <param name="query">Original query</param>
    /// <param name="webResults">Results from web search</param>
    /// <returns>Confidence score for web results</returns>
    ConfidenceScore ScoreWebSearch(string query, WebSearchResponse webResults);

    /// <summary>
    /// Detects conflicts between responses from different sources.
    /// Uses semantic similarity and logical contradiction detection.
    /// </summary>
    /// <param name="responses">List of (SourceType, Response) tuples</param>
    /// <returns>Conflict analysis with severity and resolution suggestions</returns>
    Task<ConflictAnalysis> DetectConflictsAsync(List<(SourceType SourceType, string Response)> responses);

    /// <summary>
    /// Resolves conflicts using Microsoft Docs as tie-breaker or other strategies.
    /// </summary>
    /// <param name="conflict">Conflict analysis result</param>
    /// <param name="tieBreaker">Tie-breaker source and response (typically MS Docs)</param>
    /// <returns>Resolved response text</returns>
    string ResolveConflict(ConflictAnalysis conflict, (SourceType SourceType, string Response) tieBreaker);
}

/// <summary>
/// Types of information sources in the cascade.
/// </summary>
public enum SourceType
{
    /// <summary>
    /// Local knowledge base (Azure AI Search)
    /// </summary>
    KnowledgeBase,

    /// <summary>
    /// LLM's built-in knowledge
    /// </summary>
    LLM,

    /// <summary>
    /// Microsoft Learn documentation
    /// </summary>
    MicrosoftDocs,

    /// <summary>
    /// General web search
    /// </summary>
    WebSearch
}

/// <summary>
/// Confidence score for a response with reasoning.
/// </summary>
public class ConfidenceScore
{
    /// <summary>
    /// Source type that generated this score
    /// </summary>
    public SourceType Source { get; set; }

    /// <summary>
    /// Confidence level (0.0-1.0)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Explanation of how the score was calculated
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Whether this score meets the threshold for early stopping
    /// </summary>
    public bool MeetsThreshold { get; set; }

    /// <summary>
    /// Threshold value used for comparison
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// Additional metrics used in scoring
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new();

    /// <summary>
    /// Timestamp when score was calculated
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Analysis of conflicts between different source responses.
/// </summary>
public class ConflictAnalysis
{
    /// <summary>
    /// Whether conflicts were detected
    /// </summary>
    public bool HasConflicts { get; set; }

    /// <summary>
    /// Severity of conflicts (Low, Medium, High)
    /// </summary>
    public ConflictSeverity Severity { get; set; }

    /// <summary>
    /// Sources involved in the conflict
    /// </summary>
    public List<SourceType> ConflictingSources { get; set; } = new();

    /// <summary>
    /// Detailed description of the conflicts
    /// </summary>
    public List<string> ConflictDescriptions { get; set; } = new();

    /// <summary>
    /// Semantic similarity scores between conflicting responses
    /// </summary>
    public Dictionary<string, double> SimilarityScores { get; set; } = new();

    /// <summary>
    /// Recommended resolution strategy
    /// </summary>
    public ConflictResolutionStrategy RecommendedStrategy { get; set; }

    /// <summary>
    /// Additional metadata for conflict resolution
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Severity level of conflicts between sources.
/// </summary>
public enum ConflictSeverity
{
    /// <summary>
    /// Minor differences in phrasing or details
    /// </summary>
    Low,

    /// <summary>
    /// Moderate differences in approach or emphasis
    /// </summary>
    Medium,

    /// <summary>
    /// Major contradictions in facts or recommendations
    /// </summary>
    High
}

/// <summary>
/// Strategies for resolving conflicts between sources.
/// </summary>
public enum ConflictResolutionStrategy
{
    /// <summary>
    /// Use Microsoft Docs as authoritative source
    /// </summary>
    PreferMicrosoftDocs,

    /// <summary>
    /// Use source with highest confidence score
    /// </summary>
    PreferHighestConfidence,

    /// <summary>
    /// Merge non-conflicting parts from all sources
    /// </summary>
    MergeResponses,

    /// <summary>
    /// Present all conflicting responses to user
    /// </summary>
    ShowAllSources,

    /// <summary>
    /// Use most recent source information
    /// </summary>
    PreferRecent
}
