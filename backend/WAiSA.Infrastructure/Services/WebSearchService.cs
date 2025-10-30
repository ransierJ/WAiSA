using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using WAiSA.Core.Interfaces;
using WebSearchResponse = WAiSA.Core.Interfaces.WebSearchResponse;
using WebSearchResult = WAiSA.Core.Interfaces.WebSearchResult;
using WebSearchOptions = WAiSA.Core.Interfaces.WebSearchOptions;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// Service for performing general web searches using Google Custom Search API.
/// This is the final fallback in the cascade.
/// </summary>
public class WebSearchService : IWebSearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebSearchService> _logger;
    private readonly string _googleApiKey;
    private readonly string _searchEngineId;
    private const string GoogleSearchApiUrl = "https://www.googleapis.com/customsearch/v1";

    public WebSearchService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<WebSearchService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _googleApiKey = configuration["GoogleSearch:ApiKey"]
            ?? throw new InvalidOperationException("GoogleSearch:ApiKey configuration is missing");

        _searchEngineId = configuration["GoogleSearch:WebSearchEngineId"]
            ?? throw new InvalidOperationException("GoogleSearch:WebSearchEngineId configuration is missing");
    }

    public async Task<WebSearchResponse> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        return await SearchWithOptionsAsync(query, new WebSearchOptions { MaxResults = maxResults }, cancellationToken);
    }

    public async Task<WebSearchResponse> SearchWithOptionsAsync(
        string query,
        WebSearchOptions options,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Performing web search for query: {Query}, maxResults: {MaxResults}",
                query, options.MaxResults);

            // Build Google Custom Search API request
            var requestUrl = BuildSearchUrl(query, options);

            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadFromJsonAsync<GoogleSearchResponse>(cancellationToken);

            if (jsonResponse?.Items == null || !jsonResponse.Items.Any())
            {
                _logger.LogWarning("No web search results found for query: {Query}", query);
                return new WAiSA.Core.Interfaces.WebSearchResponse
                {
                    Query = query,
                    Results = new List<WAiSA.Core.Interfaces.WebSearchResult>(),
                    OverallConfidence = 0.0,
                    ExecutionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
                };
            }

            // Convert Google results to web search results
            var results = jsonResponse.Items.Select((item, index) => new WAiSA.Core.Interfaces.WebSearchResult
            {
                Title = item.Title ?? string.Empty,
                Url = item.Link ?? string.Empty,
                Snippet = item.Snippet ?? string.Empty,
                Domain = ExtractDomain(item.Link),
                RelevanceScore = CalculateRelevanceScore(index, jsonResponse.Items.Count, item.Link),
                PublishedDate = TryParseDate(item.Pagemap?.Metatags?.FirstOrDefault()?.ArticlePublishedTime)
            }).ToList();

            // Calculate overall confidence based on result quality
            var overallConfidence = CalculateOverallConfidence(results, query);

            var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation("Web search completed. Found {Count} results with confidence {Confidence:F2} in {Ms}ms",
                results.Count, overallConfidence, executionTime);

            return new WAiSA.Core.Interfaces.WebSearchResponse
            {
                Query = query,
                Results = results,
                OverallConfidence = overallConfidence,
                ExecutionTimeMs = executionTime,
                TotalResults = (int)(jsonResponse.SearchInformation?.TotalResults ?? results.Count),
                Metadata = new Dictionary<string, string>
                {
                    { "SearchEngine", "Google Custom Search" },
                    { "SafeSearch", options.SafeSearch },
                    { "Language", options.Language ?? "en" }
                }
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while performing web search for query: {Query}", query);
            throw new InvalidOperationException($"Failed to perform web search: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing web search for query: {Query}", query);
            throw;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Performing health check for web search service");

            // Simple test query
            var testUrl = $"{GoogleSearchApiUrl}?key={_googleApiKey}&cx={_searchEngineId}&q=test&num=1";

            var response = await _httpClient.GetAsync(testUrl, cancellationToken);
            var isHealthy = response.IsSuccessStatusCode;

            _logger.LogInformation("Web search service health check: {Status}",
                isHealthy ? "Healthy" : "Unhealthy");

            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for web search service");
            return false;
        }
    }

    private string BuildSearchUrl(string query, WebSearchOptions options)
    {
        var url = $"{GoogleSearchApiUrl}?key={_googleApiKey}&cx={_searchEngineId}" +
                  $"&q={Uri.EscapeDataString(query)}&num={Math.Min(options.MaxResults, 10)}";

        // Add domain filters
        if (options.IncludeDomains?.Any() == true)
        {
            var siteQuery = string.Join(" OR ", options.IncludeDomains.Select(d => $"site:{d}"));
            url += $"&as_sitesearch={Uri.EscapeDataString(siteQuery)}";
        }

        if (options.ExcludeDomains?.Any() == true)
        {
            foreach (var domain in options.ExcludeDomains)
            {
                url += $"&as_eq={Uri.EscapeDataString($"-site:{domain}")}";
            }
        }

        // Add date range filter
        if (!string.IsNullOrWhiteSpace(options.DateRange))
        {
            url += $"&dateRestrict={Uri.EscapeDataString(options.DateRange)}";
        }

        // Add language filter
        if (!string.IsNullOrWhiteSpace(options.Language))
        {
            url += $"&lr=lang_{Uri.EscapeDataString(options.Language)}";
        }

        // Add safe search
        if (!string.IsNullOrWhiteSpace(options.SafeSearch) && options.SafeSearch.ToLower() != "off")
        {
            url += $"&safe={Uri.EscapeDataString(options.SafeSearch)}";
        }

        return url;
    }

    private string ExtractDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return string.Empty;
        }
    }

    private double CalculateRelevanceScore(int position, int totalResults, string? url)
    {
        // Base score on position
        var positionScore = position == 0 ? 1.0 : 1.0 - (Math.Log(position + 1) / Math.Log(totalResults + 1)) * 0.5;

        // Adjust based on domain authority (simple heuristic)
        var domainBoost = 1.0;
        var domain = ExtractDomain(url);

        // Trusted technical domains get a boost
        if (IsTrustedDomain(domain))
        {
            domainBoost = 1.15;
        }

        var score = Math.Min(positionScore * domainBoost, 1.0);
        return Math.Max(0.4, score); // Minimum score of 0.4 for web results
    }

    private bool IsTrustedDomain(string domain)
    {
        var trustedDomains = new[]
        {
            "stackoverflow.com",
            "github.com",
            "microsoft.com",
            "docs.microsoft.com",
            "learn.microsoft.com",
            "developer.mozilla.org",
            "wikipedia.org",
            "medium.com"
        };

        return trustedDomains.Any(td => domain.EndsWith(td, StringComparison.OrdinalIgnoreCase));
    }

    private double CalculateOverallConfidence(List<WAiSA.Core.Interfaces.WebSearchResult> results, string query)
    {
        if (!results.Any()) return 0.0;

        // Base confidence on:
        // 1. Number of results
        // 2. Top result relevance
        // 3. Domain diversity
        // 4. Presence of trusted domains

        var resultCountFactor = Math.Min(results.Count / 5.0, 1.0);
        var topResultScore = results.First().RelevanceScore;
        var avgScore = results.Average(r => r.RelevanceScore);

        // Domain diversity (more diverse = more confident)
        var uniqueDomains = results.Select(r => r.Domain).Distinct().Count();
        var diversityFactor = Math.Min(uniqueDomains / 3.0, 1.0);

        // Trusted domain presence
        var trustedCount = results.Count(r => IsTrustedDomain(r.Domain));
        var trustedFactor = Math.Min(trustedCount / 2.0, 1.0);

        // Weighted combination
        var confidence = (resultCountFactor * 0.2) +
                        (topResultScore * 0.3) +
                        (avgScore * 0.2) +
                        (diversityFactor * 0.15) +
                        (trustedFactor * 0.15);

        // Web search is less authoritative, so cap slightly lower
        confidence = Math.Min(confidence * 0.95, 0.95);

        return Math.Round(confidence, 3);
    }

    private DateTime? TryParseDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString)) return null;

        if (DateTime.TryParse(dateString, out var date))
            return date;

        return null;
    }

    #region Google Search API Response Models

    private class GoogleSearchResponse
    {
        public List<GoogleSearchItem>? Items { get; set; }
        public SearchInformation? SearchInformation { get; set; }
    }

    private class GoogleSearchItem
    {
        public string? Title { get; set; }
        public string? Link { get; set; }
        public string? Snippet { get; set; }
        public Pagemap? Pagemap { get; set; }
    }

    private class SearchInformation
    {
        public long TotalResults { get; set; }
    }

    private class Pagemap
    {
        public List<Metatags>? Metatags { get; set; }
    }

    private class Metatags
    {
        public string? ArticlePublishedTime { get; set; }
    }

    #endregion
}
