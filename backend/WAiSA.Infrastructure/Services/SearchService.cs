using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.Services;

namespace WAiSA.Infrastructure.Services;

public interface ISearchService
{
    Task<MicrosoftDocsSearchResult> SearchMicrosoftDocsAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default);
    Task<WebSearchResult> SearchWebAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default);
}

public class SearchService : ISearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SearchService> _logger;
    private readonly string? _googleApiKey;
    private readonly string? _googleSearchEngineId;
    private readonly CustomSearchAPIService? _customSearchService;

    public SearchService(
        HttpClient httpClient,
        ILogger<SearchService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Google Custom Search API configuration
        _googleApiKey = configuration["GoogleSearch:ApiKey"];
        _googleSearchEngineId = configuration["GoogleSearch:SearchEngineId"];

        if (!string.IsNullOrWhiteSpace(_googleApiKey) && !string.IsNullOrWhiteSpace(_googleSearchEngineId))
        {
            _customSearchService = new CustomSearchAPIService(new BaseClientService.Initializer
            {
                ApiKey = _googleApiKey,
                ApplicationName = "WAiSA System Administrator"
            });
            _logger.LogInformation("Google Custom Search client initialized");
        }
        else
        {
            _logger.LogWarning("Google Custom Search API key or Search Engine ID not configured");
        }
    }

    public async Task<MicrosoftDocsSearchResult> SearchMicrosoftDocsAsync(
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Searching Microsoft Learn: {Query}", query);

            // Use Bing to search site:learn.microsoft.com
            var siteQuery = $"site:learn.microsoft.com {query}";
            var webResult = await SearchWebAsync(siteQuery, maxResults, cancellationToken);

            // Convert web results to Microsoft Docs format
            var results = webResult.Results.Select(r => new MicrosoftDocResult
            {
                Title = r.Title,
                Url = r.Url,
                Snippet = r.Description
            }).ToList();

            _logger.LogInformation("Found {Count} Microsoft Learn results for: {Query}", results.Count, query);

            return new MicrosoftDocsSearchResult
            {
                Query = query,
                Count = results.Count,
                Results = results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Microsoft Learn: {Message}", ex.Message);

            // Return empty result instead of throwing
            return new MicrosoftDocsSearchResult
            {
                Query = query,
                Count = 0,
                Results = new List<MicrosoftDocResult>()
            };
        }
    }

    public async Task<WebSearchResult> SearchWebAsync(
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_customSearchService == null || string.IsNullOrWhiteSpace(_googleSearchEngineId))
            {
                _logger.LogWarning("Google Custom Search not configured");
                return new WebSearchResult
                {
                    Query = query,
                    Count = 0,
                    Results = new List<WebResult>()
                };
            }

            _logger.LogInformation("Searching web via Google: {Query}", query);

            var listRequest = _customSearchService.Cse.List();
            listRequest.Q = query;
            listRequest.Cx = _googleSearchEngineId;
            listRequest.Num = maxResults;

            var searchResponse = await listRequest.ExecuteAsync(cancellationToken);

            var results = new List<WebResult>();
            if (searchResponse?.Items != null)
            {
                results = searchResponse.Items.Select(item => new WebResult
                {
                    Title = item.Title ?? "Untitled",
                    Url = item.Link ?? string.Empty,
                    Description = item.Snippet ?? string.Empty
                }).ToList();
            }

            _logger.LogInformation("Found {Count} web results for: {Query}", results.Count, query);

            return new WebSearchResult
            {
                Query = query,
                Count = results.Count,
                Results = results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching web: {Message}", ex.Message);

            // Return empty result instead of throwing
            return new WebSearchResult
            {
                Query = query,
                Count = 0,
                Results = new List<WebResult>()
            };
        }
    }
}

// DTOs
public class MicrosoftDocsSearchResult
{
    public string Query { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<MicrosoftDocResult> Results { get; set; } = new();
}

public class MicrosoftDocResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
}

public class WebSearchResult
{
    public string Query { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<WebResult> Results { get; set; } = new();
}

public class WebResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
