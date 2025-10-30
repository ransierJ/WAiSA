using WAiSA.Shared.Models;

namespace WAiSA.Core.Interfaces;

/// <summary>
/// Service for searching and retrieving content from Microsoft Learn documentation.
/// Uses direct API calls to Microsoft Learn with confidence scoring.
/// </summary>
public interface IMicrosoftDocsService
{
    /// <summary>
    /// Searches Microsoft Learn documentation for relevant articles.
    /// Uses Google Custom Search API with site:learn.microsoft.com filter.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="maxResults">Maximum number of results to return (default 5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results with confidence scores</returns>
    Task<MicrosoftDocsSearchResponse> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the full content of a Microsoft Learn article.
    /// </summary>
    /// <param name="url">URL of the Microsoft Learn article</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Article content as plain text</returns>
    Task<string> FetchArticleContentAsync(
        string url,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if Microsoft Docs service is available and responding.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if service is healthy</returns>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Response from Microsoft Docs search containing results and metadata.
/// </summary>
public class MicrosoftDocsSearchResponse
{
    /// <summary>
    /// Original search query
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// List of search results
    /// </summary>
    public List<MicrosoftDocsResult> Results { get; set; } = new();

    /// <summary>
    /// Overall confidence score for this search (0.0-1.0)
    /// </summary>
    public double OverallConfidence { get; set; }

    /// <summary>
    /// Indicates if results are from authoritative Microsoft source
    /// </summary>
    public bool IsAuthoritative { get; set; } = true;

    /// <summary>
    /// Execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Total number of results found
    /// </summary>
    public int TotalResults { get; set; }
}

/// <summary>
/// Individual Microsoft Docs search result.
/// </summary>
public class MicrosoftDocsResult
{
    /// <summary>
    /// Article title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL to the Microsoft Learn article
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Article snippet/excerpt
    /// </summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>
    /// Relevance score for this result (0.0-1.0)
    /// </summary>
    public double RelevanceScore { get; set; }

    /// <summary>
    /// Last modified date of the article
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    /// Full article content (populated if fetched)
    /// </summary>
    public string? FullContent { get; set; }
}
