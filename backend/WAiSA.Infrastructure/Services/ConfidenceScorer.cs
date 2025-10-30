using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using WAiSA.Core.Interfaces;
using WAiSA.Shared.Models;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// Service for scoring confidence levels and detecting conflicts between sources.
/// </summary>
public class ConfidenceScorer : IConfidenceScorer
{
    private readonly ILogger<ConfidenceScorer> _logger;

    public ConfidenceScorer(ILogger<ConfidenceScorer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConfidenceScore> ScoreResponseAsync(
        string query,
        string response,
        SourceType sourceType,
        Dictionary<string, object>? metadata = null)
    {
        _logger.LogInformation("Scoring response from {SourceType} for query: {Query}",
            sourceType, query);

        return sourceType switch
        {
            SourceType.KnowledgeBase => ScoreKnowledgeBase(query,
                metadata?["results"] as List<KnowledgeSearchResult> ?? new List<KnowledgeSearchResult>()),
            SourceType.MicrosoftDocs => ScoreMicrosoftDocs(query,
                metadata?["results"] as MicrosoftDocsSearchResponse ?? new MicrosoftDocsSearchResponse()),
            SourceType.WebSearch => ScoreWebSearch(query,
                metadata?["results"] as WebSearchResponse ?? new WebSearchResponse()),
            SourceType.LLM => await ScoreLLMResponseAsync(query, response, metadata),
            _ => throw new ArgumentOutOfRangeException(nameof(sourceType))
        };
    }

    public ConfidenceScore ScoreKnowledgeBase(string query, List<KnowledgeSearchResult> knowledgeResults)
    {
        if (!knowledgeResults.Any())
        {
            return new ConfidenceScore
            {
                Source = SourceType.KnowledgeBase,
                Score = 0.0,
                Reasoning = "No knowledge base results found",
                MeetsThreshold = false,
                Threshold = 0.85
            };
        }

        // Factors for KB confidence:
        // 1. Number of results above min threshold
        // 2. Highest score
        // 3. Average score of top 3
        // 4. Score distribution consistency

        var highQualityResults = knowledgeResults.Where(r => r.SimilarityScore >= 0.7).ToList();
        var topScore = knowledgeResults.Max(r => r.SimilarityScore);
        var top3Average = knowledgeResults.Take(3).Average(r => r.SimilarityScore);
        var scoreVariance = CalculateVariance(knowledgeResults.Select(r => r.SimilarityScore).ToList());

        // Weighted calculation
        var resultCountFactor = Math.Min(highQualityResults.Count / 3.0, 1.0);
        var consistencyFactor = 1.0 - Math.Min(scoreVariance, 0.3);

        var confidence = (topScore * 0.4) +
                        (top3Average * 0.3) +
                        (resultCountFactor * 0.2) +
                        (consistencyFactor * 0.1);

        var threshold = 0.85;
        var meetsThreshold = confidence >= threshold;

        return new ConfidenceScore
        {
            Source = SourceType.KnowledgeBase,
            Score = Math.Round(confidence, 3),
            Reasoning = $"KB: {highQualityResults.Count} high-quality results, top score {topScore:F2}, avg {top3Average:F2}",
            MeetsThreshold = meetsThreshold,
            Threshold = threshold,
            Metrics = new Dictionary<string, double>
            {
                { "TopScore", topScore },
                { "AverageScore", top3Average },
                { "HighQualityCount", (double)highQualityResults.Count },
                { "ScoreVariance", scoreVariance }
            }
        };
    }

    public ConfidenceScore ScoreMicrosoftDocs(string query, MicrosoftDocsSearchResponse docsResults)
    {
        if (!docsResults.Results.Any())
        {
            return new ConfidenceScore
            {
                Source = SourceType.MicrosoftDocs,
                Score = 0.0,
                Reasoning = "No Microsoft Docs results found",
                MeetsThreshold = false,
                Threshold = 0.80
            };
        }

        // Microsoft Docs is authoritative, so higher base confidence
        // Factors:
        // 1. Result count and relevance
        // 2. Article recency (more recent = higher confidence)
        // 3. Domain authority (always learn.microsoft.com = high)

        var avgRelevance = docsResults.Results.Average(r => r.RelevanceScore);
        var topRelevance = docsResults.Results.First().RelevanceScore;

        // Recency bonus (articles from last year get boost)
        var recencyBonus = 0.0;
        if (docsResults.Results.Any(r => r.LastModified.HasValue))
        {
            var recentArticles = docsResults.Results
                .Where(r => r.LastModified.HasValue && r.LastModified.Value > DateTime.UtcNow.AddYears(-1))
                .Count();
            recencyBonus = Math.Min(recentArticles / 3.0, 0.1);
        }

        // Base confidence from overall response
        var confidence = docsResults.OverallConfidence;

        // Apply recency bonus
        confidence = Math.Min(confidence + recencyBonus, 1.0);

        var threshold = 0.80;
        var meetsThreshold = confidence >= threshold;

        return new ConfidenceScore
        {
            Source = SourceType.MicrosoftDocs,
            Score = Math.Round(confidence, 3),
            Reasoning = $"MS Docs: {docsResults.Results.Count} results, top relevance {topRelevance:F2}, authoritative source",
            MeetsThreshold = meetsThreshold,
            Threshold = threshold,
            Metrics = new Dictionary<string, double>
            {
                { "ResultCount", docsResults.Results.Count },
                { "TopRelevance", topRelevance },
                { "AvgRelevance", avgRelevance },
                { "RecencyBonus", recencyBonus }
            }
        };
    }

    public ConfidenceScore ScoreWebSearch(string query, WebSearchResponse webResults)
    {
        if (!webResults.Results.Any())
        {
            return new ConfidenceScore
            {
                Source = SourceType.WebSearch,
                Score = 0.0,
                Reasoning = "No web search results found",
                MeetsThreshold = false,
                Threshold = 0.70
            };
        }

        // Web search is least authoritative
        // Use the overall confidence from the service
        var confidence = webResults.OverallConfidence;

        var threshold = 0.70;
        var meetsThreshold = confidence >= threshold;

        return new ConfidenceScore
        {
            Source = SourceType.WebSearch,
            Score = Math.Round(confidence, 3),
            Reasoning = $"Web: {webResults.Results.Count} results, diverse sources, lower authority",
            MeetsThreshold = meetsThreshold,
            Threshold = threshold,
            Metrics = new Dictionary<string, double>
            {
                { "ResultCount", webResults.Results.Count },
                { "TopRelevance", webResults.Results.FirstOrDefault()?.RelevanceScore ?? 0 }
            }
        };
    }

    private async Task<ConfidenceScore> ScoreLLMResponseAsync(
        string query,
        string response,
        Dictionary<string, object>? metadata)
    {
        // For LLM responses, we use several heuristics:
        // 1. Response length (too short = uncertain, too long = verbose)
        // 2. Presence of uncertainty markers ("I think", "maybe", "probably")
        // 3. Specific vs vague language
        // 4. Code examples or concrete details
        // 5. CONVERSATIONAL CONTEXT: Detect conversational/memory statements

        var lengthScore = ScoreLengthAppropriate(response);
        var certaintyScore = ScoreCertainty(response);
        var specificityScore = ScoreSpecificity(response);
        var conversationalBonus = ScoreConversationalContext(query, response);

        // If this is a conversational response, prioritize it higher
        var confidence = conversationalBonus > 0.5
            ? Math.Min((lengthScore * 0.2) + (certaintyScore * 0.4) + (specificityScore * 0.1) + (conversationalBonus * 0.3), 1.0)
            : (lengthScore * 0.3) + (certaintyScore * 0.5) + (specificityScore * 0.2);

        var threshold = 0.75;
        var meetsThreshold = confidence >= threshold;

        return new ConfidenceScore
        {
            Source = SourceType.LLM,
            Score = Math.Round(confidence, 3),
            Reasoning = conversationalBonus > 0.5
                ? $"LLM: Conversational response (bonus {conversationalBonus:F2}), certainty {certaintyScore:F2}"
                : $"LLM: Certainty {certaintyScore:F2}, specificity {specificityScore:F2}, length appropriate {lengthScore:F2}",
            MeetsThreshold = meetsThreshold,
            Threshold = threshold,
            Metrics = new Dictionary<string, double>
            {
                { "LengthScore", lengthScore },
                { "CertaintyScore", certaintyScore },
                { "SpecificityScore", specificityScore },
                { "ConversationalBonus", conversationalBonus },
                { "ResponseLength", response.Length }
            }
        };
    }

    public async Task<ConflictAnalysis> DetectConflictsAsync(
        List<(SourceType SourceType, string Response)> responses)
    {
        if (responses.Count < 2)
        {
            return new ConflictAnalysis
            {
                HasConflicts = false,
                Severity = ConflictSeverity.Low,
                RecommendedStrategy = ConflictResolutionStrategy.PreferHighestConfidence
            };
        }

        _logger.LogInformation("Detecting conflicts between {Count} source responses", responses.Count);

        var conflicts = new List<string>();
        var conflictingSources = new List<SourceType>();
        var similarityScores = new Dictionary<string, double>();

        // Compare each pair of responses
        for (int i = 0; i < responses.Count - 1; i++)
        {
            for (int j = i + 1; j < responses.Count; j++)
            {
                var source1 = responses[i].SourceType;
                var source2 = responses[j].SourceType;
                var response1 = responses[i].Response;
                var response2 = responses[j].Response;

                // Calculate semantic similarity (simplified)
                var similarity = CalculateTextSimilarity(response1, response2);
                var pairKey = $"{source1}-{source2}";
                similarityScores[pairKey] = similarity;

                // Low similarity might indicate conflict
                if (similarity < 0.5)
                {
                    conflicts.Add($"Low similarity ({similarity:F2}) between {source1} and {source2}");
                    if (!conflictingSources.Contains(source1)) conflictingSources.Add(source1);
                    if (!conflictingSources.Contains(source2)) conflictingSources.Add(source2);
                }

                // Check for explicit contradictions
                if (ContainsContradiction(response1, response2))
                {
                    conflicts.Add($"Contradictory statements between {source1} and {source2}");
                    if (!conflictingSources.Contains(source1)) conflictingSources.Add(source1);
                    if (!conflictingSources.Contains(source2)) conflictingSources.Add(source2);
                }
            }
        }

        var hasConflicts = conflicts.Any();
        var severity = DetermineSeverity(conflicts, similarityScores);

        return new ConflictAnalysis
        {
            HasConflicts = hasConflicts,
            Severity = severity,
            ConflictingSources = conflictingSources,
            ConflictDescriptions = conflicts,
            SimilarityScores = similarityScores,
            RecommendedStrategy = hasConflicts
                ? ConflictResolutionStrategy.PreferMicrosoftDocs
                : ConflictResolutionStrategy.PreferHighestConfidence
        };
    }

    public string ResolveConflict(ConflictAnalysis conflict, (SourceType SourceType, string Response) tieBreaker)
    {
        if (!conflict.HasConflicts)
        {
            _logger.LogInformation("No conflicts to resolve");
            return tieBreaker.Response;
        }

        _logger.LogInformation("Resolving conflict using {Strategy} strategy with tie-breaker {Source}",
            conflict.RecommendedStrategy, tieBreaker.SourceType);

        // Use Microsoft Docs as authoritative tie-breaker
        if (tieBreaker.SourceType == SourceType.MicrosoftDocs)
        {
            _logger.LogInformation("Using Microsoft Docs as authoritative source for conflict resolution");
            return tieBreaker.Response;
        }

        // Fallback to the tie-breaker response
        return tieBreaker.Response;
    }

    #region Helper Methods

    private double ScoreLengthAppropriate(string response)
    {
        var length = response.Length;

        // Optimal length is 200-2000 characters
        if (length < 50) return 0.3; // Too short
        if (length < 200) return 0.7; // Acceptable
        if (length <= 2000) return 1.0; // Optimal
        if (length <= 4000) return 0.8; // Verbose but acceptable
        return 0.6; // Too verbose
    }

    private double ScoreCertainty(string response)
    {
        var uncertaintyMarkers = new[]
        {
            "i think", "maybe", "probably", "might", "could be", "possibly",
            "i'm not sure", "uncertain", "unclear", "don't know"
        };

        var lowerResponse = response.ToLower();
        var uncertaintyCount = uncertaintyMarkers.Count(marker => lowerResponse.Contains(marker));

        // More uncertainty markers = lower score
        if (uncertaintyCount == 0) return 1.0;
        if (uncertaintyCount == 1) return 0.8;
        if (uncertaintyCount == 2) return 0.6;
        return 0.4;
    }

    private double ScoreSpecificity(string response)
    {
        // Check for specific indicators: code blocks, numbers, URLs, concrete examples
        var hasCodeBlock = response.Contains("```") || response.Contains("()") || response.Contains("{}");
        var hasNumbers = Regex.IsMatch(response, @"\d+");
        var hasUrl = Regex.IsMatch(response, @"https?://");
        var hasExamples = response.ToLower().Contains("example") || response.ToLower().Contains("for instance");

        var specificityScore = 0.5; // Base score

        if (hasCodeBlock) specificityScore += 0.2;
        if (hasNumbers) specificityScore += 0.1;
        if (hasUrl) specificityScore += 0.1;
        if (hasExamples) specificityScore += 0.1;

        return Math.Min(specificityScore, 1.0);
    }

    private double ScoreConversationalContext(string query, string response)
    {
        // Detect conversational/memory statements that should be prioritized
        // These are short, acknowledgment-style responses that set context or recall information

        var lowerQuery = query.ToLower();
        var lowerResponse = response.ToLower();

        // Check if the query is requesting context/memory operations
        var isMemoryQuery = lowerQuery.Contains("remember") ||
                            lowerQuery.Contains("what did i") ||
                            lowerQuery.Contains("you said") ||
                            lowerQuery.Contains("told you") ||
                            lowerQuery.Contains("mentioned") ||
                            lowerQuery.Contains("earlier") ||
                            lowerQuery.Contains("my name");

        // Check if the response is a conversational acknowledgment
        var isAcknowledgment = lowerResponse.Contains("got it") ||
                               lowerResponse.Contains("okay") ||
                               lowerResponse.Contains("sure") ||
                               lowerResponse.Contains("noted") ||
                               lowerResponse.Contains("i'll remember") ||
                               lowerResponse.Contains("i remember") ||
                               lowerResponse.Contains("you told me") ||
                               lowerResponse.Contains("you said") ||
                               lowerResponse.Contains("you mentioned");

        // Short, direct responses that reference specific data
        var isShortAndDirect = response.Length < 150 && Regex.IsMatch(response, @"\d+|[A-Z][a-z]+");

        // Calculate bonus score
        var bonus = 0.0;
        if (isMemoryQuery && isAcknowledgment) bonus = 1.0; // Perfect conversational match
        else if (isMemoryQuery && isShortAndDirect) bonus = 0.9; // Memory query with specific answer
        else if (isAcknowledgment) bonus = 0.8; // General acknowledgment
        else if (isShortAndDirect && isMemoryQuery) bonus = 0.7; // Short response to memory query

        return bonus;
    }

    private double CalculateTextSimilarity(string text1, string text2)
    {
        // Simple Jaccard similarity based on word overlap
        var words1 = text1.ToLower().Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = text2.ToLower().Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (!words1.Any() || !words2.Any()) return 0.0;

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return (double)intersection / union;
    }

    private bool ContainsContradiction(string text1, string text2)
    {
        // Simple contradiction detection
        var contradictionPairs = new[]
        {
            ("should", "should not"),
            ("can", "cannot"),
            ("is", "is not"),
            ("will", "will not"),
            ("must", "must not"),
            ("true", "false"),
            ("yes", "no"),
            ("correct", "incorrect")
        };

        var lower1 = text1.ToLower();
        var lower2 = text2.ToLower();

        foreach (var (positive, negative) in contradictionPairs)
        {
            if ((lower1.Contains(positive) && lower2.Contains(negative)) ||
                (lower1.Contains(negative) && lower2.Contains(positive)))
            {
                return true;
            }
        }

        return false;
    }

    private ConflictSeverity DetermineSeverity(List<string> conflicts, Dictionary<string, double> similarityScores)
    {
        if (!conflicts.Any()) return ConflictSeverity.Low;

        // Check for explicit contradictions
        var hasContradictions = conflicts.Any(c => c.Contains("Contradictory"));

        // Check similarity scores
        var avgSimilarity = similarityScores.Values.Any() ? similarityScores.Values.Average() : 1.0;

        if (hasContradictions || avgSimilarity < 0.3) return ConflictSeverity.High;
        if (avgSimilarity < 0.5) return ConflictSeverity.Medium;
        return ConflictSeverity.Low;
    }

    private double CalculateVariance(List<double> values)
    {
        if (!values.Any()) return 0.0;

        var mean = values.Average();
        var sumSquaredDiff = values.Sum(v => Math.Pow(v - mean, 2));
        return sumSquaredDiff / values.Count;
    }

    #endregion
}
