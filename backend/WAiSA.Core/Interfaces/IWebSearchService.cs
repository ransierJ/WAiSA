using WAiSA.Shared.Models;

namespace WAiSA.Core.Interfaces;

/// <summary>
/// Service for performing general web searches using Google Custom Search API.
/// This is the final fallback in the cascade when KB, LLM, and MS Docs don't provide sufficient answers.
/// </summary>
public interface IWebSearchService
{
    /// <summary>
    /// Performs a web search using Google Custom Search API.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="maxResults">Maximum number of results to return (default 5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results with confidence scores</returns>
    Task<WebSearchResponse> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a web search with additional filters and options.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="options">Search options (domain filters, date ranges, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results with confidence scores</returns>
    Task<WebSearchResponse> SearchWithOptionsAsync(
        string query,
        WebSearchOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if web search service is available and responding.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if service is healthy</returns>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Response from web search containing results and metadata.
/// </summary>
public class WebSearchResponse
{
    /// <summary>
    /// Original search query
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// List of search results
    /// </summary>
    public List<WebSearchResult> Results { get; set; } = new();

    /// <summary>
    /// Overall confidence score for this search (0.0-1.0)
    /// </summary>
    public double OverallConfidence { get; set; }

    /// <summary>
    /// Execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Total number of results found
    /// </summary>
    public int TotalResults { get; set; }

    /// <summary>
    /// Search metadata (API used, filters applied, etc.)
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Individual web search result.
/// </summary>
public class WebSearchResult
{
    /// <summary>
    /// Page title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL to the web page
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Page snippet/excerpt
    /// </summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>
    /// Relevance score for this result (0.0-1.0)
    /// </summary>
    public double RelevanceScore { get; set; }

    /// <summary>
    /// Domain name of the result
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Last modified or published date
    /// </summary>
    public DateTime? PublishedDate { get; set; }
}

/// <summary>
/// Options for web search queries.
/// </summary>
public class WebSearchOptions
{
    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    public int MaxResults { get; set; } = 5;

    /// <summary>
    /// Filter to specific domains (e.g., "microsoft.com", "stackoverflow.com")
    /// </summary>
    public List<string>? IncludeDomains { get; set; }

    /// <summary>
    /// Exclude specific domains
    /// </summary>
    public List<string>? ExcludeDomains { get; set; }

    /// <summary>
    /// Filter by date range (e.g., "past_week", "past_month", "past_year")
    /// </summary>
    public string? DateRange { get; set; }

    /// <summary>
    /// Filter by language (e.g., "en", "es", "fr")
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Safe search level ("off", "medium", "high")
    /// </summary>
    public string SafeSearch { get; set; } = "medium";
}
