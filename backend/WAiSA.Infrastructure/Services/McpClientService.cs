using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace WAiSA.Infrastructure.Services;

public interface IMcpClientService
{
    Task<McpSearchResult> SearchDocumentationAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default);
    Task<McpFetchResult> FetchDocumentationAsync(string url, CancellationToken cancellationToken = default);
    Task<McpCodeSamplesResult> SearchCodeSamplesAsync(string query, string? language = null, int maxResults = 10, CancellationToken cancellationToken = default);
    Task<List<McpTool>> GetAvailableToolsAsync(CancellationToken cancellationToken = default);
}

public class McpClientService : IMcpClientService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<McpClientService> _logger;
    private readonly string _mcpBridgeUrl;

    public McpClientService(
        HttpClient httpClient,
        ILogger<McpClientService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _mcpBridgeUrl = configuration["McpBridge:Url"] ?? "http://localhost:3001";

        _logger.LogInformation("MCP Client initialized with bridge URL: {Url}", _mcpBridgeUrl);
    }

    public async Task<McpSearchResult> SearchDocumentationAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Searching Microsoft Learn: {Query}", query);

            var request = new
            {
                query,
                maxResults
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_mcpBridgeUrl}/api/docs/search",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<McpSearchResult>(cancellationToken: cancellationToken);

            if (result == null)
            {
                _logger.LogWarning("Received null response from MCP bridge");
                return new McpSearchResult
                {
                    Query = query,
                    Count = 0,
                    Results = new List<DocResult>()
                };
            }

            _logger.LogInformation("Found {Count} results for query: {Query}", result.Count, query);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documentation: {Message}", ex.Message);
            throw new McpClientException("Failed to search Microsoft Learn documentation", ex);
        }
    }

    public async Task<McpFetchResult> FetchDocumentationAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching documentation from: {Url}", url);

            var request = new { url };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_mcpBridgeUrl}/api/docs/fetch",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<McpFetchResult>(cancellationToken: cancellationToken);

            if (result == null)
            {
                _logger.LogWarning("Received null response from MCP bridge");
                return new McpFetchResult
                {
                    Url = url,
                    Content = string.Empty,
                    ContentLength = 0
                };
            }

            _logger.LogInformation("Fetched {Length} characters from: {Url}", result.ContentLength, url);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching documentation: {Message}", ex.Message);
            throw new McpClientException("Failed to fetch Microsoft Learn documentation", ex);
        }
    }

    public async Task<McpCodeSamplesResult> SearchCodeSamplesAsync(
        string query,
        string? language = null,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Searching code samples: {Query} (Language: {Language})", query, language ?? "any");

            var request = new
            {
                query,
                language,
                maxResults
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_mcpBridgeUrl}/api/docs/code-samples",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<McpCodeSamplesResult>(cancellationToken: cancellationToken);

            if (result == null)
            {
                _logger.LogWarning("Received null response from MCP bridge");
                return new McpCodeSamplesResult
                {
                    Query = query,
                    Language = language,
                    Count = 0,
                    Results = new List<CodeSampleResult>()
                };
            }

            _logger.LogInformation("Found {Count} code samples for query: {Query}", result.Count, query);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching code samples: {Message}", ex.Message);
            throw new McpClientException("Failed to search Microsoft Learn code samples", ex);
        }
    }

    public async Task<List<McpTool>> GetAvailableToolsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching available MCP tools");

            var response = await _httpClient.GetAsync(
                $"{_mcpBridgeUrl}/api/tools",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<McpToolsResponse>(cancellationToken: cancellationToken);

            if (result == null || result.Tools == null)
            {
                _logger.LogWarning("Received null response from MCP bridge");
                return new List<McpTool>();
            }

            _logger.LogInformation("Retrieved {Count} available MCP tools", result.Tools.Count);
            return result.Tools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching available tools: {Message}", ex.Message);
            throw new McpClientException("Failed to fetch available MCP tools", ex);
        }
    }
}

// DTOs
public class McpSearchResult
{
    public string Query { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<DocResult> Results { get; set; } = new();
}

public class DocResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public string? LastUpdated { get; set; }
}

public class McpFetchResult
{
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int ContentLength { get; set; }
}

public class McpCodeSamplesResult
{
    public string Query { get; set; } = string.Empty;
    public string? Language { get; set; }
    public int Count { get; set; }
    public List<CodeSampleResult> Results { get; set; } = new();
}

public class CodeSampleResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
}

public class McpTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonElement InputSchema { get; set; }
}

public class McpToolsResponse
{
    public List<McpTool> Tools { get; set; } = new();
}

public class McpClientException : Exception
{
    public McpClientException(string message) : base(message) { }
    public McpClientException(string message, Exception innerException) : base(message, innerException) { }
}
