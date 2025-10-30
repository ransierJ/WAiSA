using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using WAiSA.Core.Interfaces;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// Service for searching Microsoft Learn documentation using Google Custom Search API.
/// </summary>
public class MicrosoftDocsService : IMicrosoftDocsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MicrosoftDocsService> _logger;
    private readonly string _googleApiKey;
    private readonly string _searchEngineId;
    private const string GoogleSearchApiUrl = "https://www.googleapis.com/customsearch/v1";

    public MicrosoftDocsService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MicrosoftDocsService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _googleApiKey = configuration["GoogleSearch:ApiKey"]
            ?? throw new InvalidOperationException("GoogleSearch:ApiKey configuration is missing");

        _searchEngineId = configuration["GoogleSearch:MicrosoftDocsEngineId"]
            ?? throw new InvalidOperationException("GoogleSearch:MicrosoftDocsEngineId configuration is missing");
    }

    public async Task<MicrosoftDocsSearchResponse> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Searching Microsoft Docs for query: {Query}, maxResults: {MaxResults}",
                query, maxResults);

            // Build Google Custom Search API request
            var requestUrl = $"{GoogleSearchApiUrl}?key={_googleApiKey}&cx={_searchEngineId}" +
                           $"&q={Uri.EscapeDataString(query)}&num={Math.Min(maxResults, 10)}" +
                           $"&siteSearch=learn.microsoft.com&siteSearchFilter=i";

            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadFromJsonAsync<GoogleSearchResponse>(cancellationToken);

            if (jsonResponse?.Items == null || !jsonResponse.Items.Any())
            {
                _logger.LogWarning("No results found for query: {Query}", query);
                return new MicrosoftDocsSearchResponse
                {
                    Query = query,
                    Results = new List<MicrosoftDocsResult>(),
                    OverallConfidence = 0.0,
                    ExecutionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
                };
            }

            // Convert Google results to Microsoft Docs results
            var results = jsonResponse.Items.Select((item, index) => new MicrosoftDocsResult
            {
                Title = item.Title ?? string.Empty,
                Url = item.Link ?? string.Empty,
                Snippet = item.Snippet ?? string.Empty,
                RelevanceScore = CalculateRelevanceScore(index, jsonResponse.Items.Count),
                LastModified = TryParseDate(item.Pagemap?.Metatags?.FirstOrDefault()?.ArticleModifiedTime)
            }).ToList();

            // Calculate overall confidence based on result quality
            var overallConfidence = CalculateOverallConfidence(results, query);

            var executionTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation("Microsoft Docs search completed. Found {Count} results with confidence {Confidence:F2} in {Ms}ms",
                results.Count, overallConfidence, executionTime);

            return new MicrosoftDocsSearchResponse
            {
                Query = query,
                Results = results,
                OverallConfidence = overallConfidence,
                IsAuthoritative = true,
                ExecutionTimeMs = executionTime,
                TotalResults = (int)(jsonResponse.SearchInformation?.TotalResults ?? results.Count)
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while searching Microsoft Docs for query: {Query}", query);
            throw new InvalidOperationException($"Failed to search Microsoft Docs: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Microsoft Docs for query: {Query}", query);
            throw;
        }
    }

    public async Task<string> FetchArticleContentAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching content from Microsoft Docs URL: {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            // Basic HTML to text conversion (would be better with a proper HTML parser)
            var textContent = System.Text.RegularExpressions.Regex.Replace(content, "<.*?>", string.Empty);
            textContent = System.Web.HttpUtility.HtmlDecode(textContent);

            _logger.LogInformation("Successfully fetched content from {Url}, length: {Length}",
                url, textContent.Length);

            return textContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching content from URL: {Url}", url);
            throw new InvalidOperationException($"Failed to fetch article content from {url}", ex);
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Performing health check for Microsoft Docs service");

            // Simple test query
            var testUrl = $"{GoogleSearchApiUrl}?key={_googleApiKey}&cx={_searchEngineId}" +
                         $"&q=azure&num=1&siteSearch=learn.microsoft.com";

            var response = await _httpClient.GetAsync(testUrl, cancellationToken);
            var isHealthy = response.IsSuccessStatusCode;

            _logger.LogInformation("Microsoft Docs service health check: {Status}",
                isHealthy ? "Healthy" : "Unhealthy");

            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for Microsoft Docs service");
            return false;
        }
    }

    private double CalculateRelevanceScore(int position, int totalResults)
    {
        // Top result gets highest score, decreasing logarithmically
        // Position 0 -> 1.0, Position 1 -> 0.9, Position 2 -> 0.85, etc.
        if (position == 0) return 1.0;
        if (totalResults == 1) return 1.0;

        // Logarithmic decay
        var score = 1.0 - (Math.Log(position + 1) / Math.Log(totalResults + 1)) * 0.5;
        return Math.Max(0.5, score); // Minimum score of 0.5 for any result
    }

    private double CalculateOverallConfidence(List<MicrosoftDocsResult> results, string query)
    {
        if (!results.Any()) return 0.0;

        // Base confidence on:
        // 1. Number of results (more = better)
        // 2. Top result relevance score
        // 3. Average relevance of top 3

        var resultCountFactor = Math.Min(results.Count / 5.0, 1.0); // Max at 5 results
        var topResultScore = results.First().RelevanceScore;
        var top3Average = results.Take(3).Average(r => r.RelevanceScore);

        // Weighted combination
        var confidence = (resultCountFactor * 0.3) + (topResultScore * 0.4) + (top3Average * 0.3);

        // Microsoft Docs is authoritative, so boost confidence slightly
        confidence = Math.Min(confidence * 1.1, 1.0);

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
        public string? ArticleModifiedTime { get; set; }
    }

    #endregion
}
