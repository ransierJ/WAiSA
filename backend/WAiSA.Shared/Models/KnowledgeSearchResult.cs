namespace WAiSA.Shared.Models;

/// <summary>
/// Knowledge search result with similarity score
/// </summary>
public class KnowledgeSearchResult
{
    /// <summary>
    /// Knowledge entry
    /// </summary>
    public KnowledgeBaseEntry Entry { get; set; } = new();

    /// <summary>
    /// Similarity score (0.0 to 1.0)
    /// </summary>
    public double SimilarityScore { get; set; }

    /// <summary>
    /// Whether this entry meets the minimum score threshold
    /// </summary>
    public bool IsRelevant => SimilarityScore >= 0.7;
}
