using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using WAiSA.Core.Interfaces;
using WAiSA.Shared.Configuration;
using WAiSA.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// Knowledge Base Service implementation using Azure AI Search with RAG
/// </summary>
public class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly AzureSearchOptions _options;
    private readonly IAIOrchestrationService _aiService;
    private readonly ILogger<KnowledgeBaseService> _logger;

    public KnowledgeBaseService(
        SearchClient searchClient,
        IOptions<AzureSearchOptions> options,
        IAIOrchestrationService aiService,
        ILogger<KnowledgeBaseService> logger)
    {
        _searchClient = searchClient;
        _options = options.Value;
        _aiService = aiService;
        _logger = logger;

        // Get index client from search client for index management operations
        var credential = new AzureKeyCredential(_options.AdminKey);
        _indexClient = new SearchIndexClient(new Uri(_options.Endpoint), credential);
    }

    /// <summary>
    /// Add or update knowledge entry in the knowledge base
    /// </summary>
    public async Task AddOrUpdateKnowledgeAsync(
        KnowledgeBaseEntry entry,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate embedding for the content
            if (entry.ContentVector.Length == 0)
            {
                _logger.LogInformation("Generating embedding for knowledge entry {Id}", entry.Id);
                entry.ContentVector = await _aiService.GenerateEmbeddingAsync(
                    entry.Content,
                    cancellationToken);
            }

            // Upload to Azure AI Search
            var batch = IndexDocumentsBatch.Upload(new[] { entry });
            await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Added/updated knowledge entry {Id}: {Title}",
                entry.Id,
                entry.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding/updating knowledge entry {Id}", entry.Id);
            throw;
        }
    }

    /// <summary>
    /// Retrieve relevant knowledge for a query using vector search
    /// </summary>
    public async Task<List<KnowledgeSearchResult>> RetrieveRelevantKnowledgeAsync(
        string query,
        int topK = 5,
        double minScore = 0.7,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving knowledge for query: {Query}", query);

            // Generate embedding for the query
            var queryVector = await _aiService.GenerateEmbeddingAsync(query, cancellationToken);

            // Perform vector search
            var vectorQuery = new VectorizedQuery(queryVector)
            {
                KNearestNeighborsCount = topK,
                Fields = { "contentVector" }
            };

            var searchOptions = new SearchOptions
            {
                VectorSearch = new()
                {
                    Queries = { vectorQuery }
                },
                Size = topK,
                Select = { "id", "title", "content", "source", "sourceDeviceId", "tags",
                          "usageCount", "averageRating", "createdAt", "updatedAt", "lastUsedAt" }
            };

            var response = await _searchClient.SearchAsync<KnowledgeBaseEntry>(
                null,
                searchOptions,
                cancellationToken);

            var results = new List<KnowledgeSearchResult>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                var score = result.Score ?? 0;

                if (score >= minScore)
                {
                    results.Add(new KnowledgeSearchResult
                    {
                        Entry = result.Document,
                        SimilarityScore = score
                    });
                }
            }

            _logger.LogInformation(
                "Retrieved {Count} relevant knowledge entries (score >= {MinScore})",
                results.Count,
                minScore);

            return results.OrderByDescending(r => r.SimilarityScore).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving relevant knowledge for query: {Query}", query);
            return new List<KnowledgeSearchResult>();
        }
    }

    /// <summary>
    /// Extract and store knowledge from a successful interaction
    /// </summary>
    public async Task<KnowledgeBaseEntry> ExtractKnowledgeFromInteractionAsync(
        Interaction interaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Extracting knowledge from interaction {Id}",
                interaction.Id);

            // Create knowledge entry from interaction
            var entry = new KnowledgeBaseEntry
            {
                Title = TruncateString(interaction.UserMessage, 100),
                Content = $"User Question: {interaction.UserMessage}\n\nAssistant Answer: {interaction.AssistantResponse}",
                Source = "user-interaction",
                SourceDeviceId = interaction.DeviceId,
                Tags = ExtractTags(interaction),
                UsageCount = 1,
                AverageRating = interaction.FeedbackRating,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await AddOrUpdateKnowledgeAsync(entry, cancellationToken);

            _logger.LogInformation(
                "Extracted knowledge from interaction {Id} -> Entry {EntryId}",
                interaction.Id,
                entry.Id);

            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting knowledge from interaction {Id}", interaction.Id);
            throw;
        }
    }

    /// <summary>
    /// Search knowledge base with filters
    /// </summary>
    public async Task<List<KnowledgeBaseEntry>> SearchKnowledgeAsync(
        string searchText,
        List<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Size = 50,
                Select = { "id", "title", "content", "source", "sourceDeviceId", "tags",
                          "usageCount", "averageRating", "createdAt", "updatedAt", "lastUsedAt" }
            };

            // Add tag filter if specified
            if (tags != null && tags.Any())
            {
                var tagFilter = string.Join(" or ", tags.Select(t => $"tags/any(tag: tag eq '{t}')"));
                searchOptions.Filter = tagFilter;
            }

            var response = await _searchClient.SearchAsync<KnowledgeBaseEntry>(
                searchText,
                searchOptions,
                cancellationToken);

            var results = new List<KnowledgeBaseEntry>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                results.Add(result.Document);
            }

            _logger.LogInformation(
                "Search returned {Count} results for: {SearchText}",
                results.Count,
                searchText);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching knowledge for: {SearchText}", searchText);
            return new List<KnowledgeBaseEntry>();
        }
    }

    /// <summary>
    /// Get all knowledge entries (paginated)
    /// </summary>
    public async Task<List<KnowledgeBaseEntry>> GetAllKnowledgeAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Skip = skip,
                Size = take,
                OrderBy = { "updatedAt desc" },
                Select = { "id", "title", "content", "source", "sourceDeviceId", "tags",
                          "usageCount", "averageRating", "createdAt", "updatedAt", "lastUsedAt" }
            };

            var response = await _searchClient.SearchAsync<KnowledgeBaseEntry>(
                "*",
                searchOptions,
                cancellationToken);

            var results = new List<KnowledgeBaseEntry>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                results.Add(result.Document);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all knowledge");
            return new List<KnowledgeBaseEntry>();
        }
    }

    /// <summary>
    /// Record knowledge usage and update statistics
    /// </summary>
    public async Task RecordKnowledgeUsageAsync(
        string knowledgeId,
        int? rating = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get existing entry
            var response = await _searchClient.GetDocumentAsync<KnowledgeBaseEntry>(
                knowledgeId,
                cancellationToken: cancellationToken);

            var entry = response.Value;
            entry.UsageCount++;
            entry.LastUsedAt = DateTime.UtcNow;

            // Update average rating if provided
            if (rating.HasValue)
            {
                if (entry.AverageRating.HasValue)
                {
                    // Calculate new average
                    var totalRating = entry.AverageRating.Value * (entry.UsageCount - 1) + rating.Value;
                    entry.AverageRating = totalRating / entry.UsageCount;
                }
                else
                {
                    entry.AverageRating = rating.Value;
                }
            }

            await AddOrUpdateKnowledgeAsync(entry, cancellationToken);

            _logger.LogInformation(
                "Recorded usage for knowledge {Id}. Total usage: {Count}",
                knowledgeId,
                entry.UsageCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording knowledge usage for {Id}", knowledgeId);
        }
    }

    /// <summary>
    /// Delete knowledge entry
    /// </summary>
    public async Task DeleteKnowledgeAsync(
        string knowledgeId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var batch = IndexDocumentsBatch.Delete("id", new[] { knowledgeId });
            await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);

            _logger.LogInformation("Deleted knowledge entry {Id}", knowledgeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting knowledge entry {Id}", knowledgeId);
            throw;
        }
    }

    /// <summary>
    /// Extract tags from interaction content
    /// </summary>
    private List<string> ExtractTags(Interaction interaction)
    {
        var tags = new List<string>();

        // Add tags based on content analysis
        var content = $"{interaction.UserMessage} {interaction.AssistantResponse}".ToLower();

        // Common Windows admin topics
        var topicKeywords = new Dictionary<string, string[]>
        {
            { "user-management", new[] { "user", "account", "password", "permissions", "administrator" } },
            { "software", new[] { "install", "uninstall", "software", "application", "program", "update" } },
            { "network", new[] { "network", "ip", "dns", "firewall", "connection", "wifi" } },
            { "performance", new[] { "performance", "slow", "memory", "cpu", "disk", "optimize" } },
            { "security", new[] { "security", "antivirus", "defender", "malware", "vulnerability" } },
            { "troubleshooting", new[] { "error", "problem", "issue", "fix", "troubleshoot", "crash" } },
            { "system", new[] { "system", "service", "process", "startup", "registry" } },
            { "files", new[] { "file", "folder", "directory", "backup", "copy", "move" } }
        };

        foreach (var (tag, keywords) in topicKeywords)
        {
            if (keywords.Any(keyword => content.Contains(keyword)))
            {
                tags.Add(tag);
            }
        }

        return tags.Any() ? tags : new List<string> { "general" };
    }

    /// <summary>
    /// Truncate string to specified length
    /// </summary>
    private string TruncateString(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// Initialize the Azure AI Search index for knowledge base
    /// Creates the index if it doesn't exist
    /// </summary>
    public async Task InitializeIndexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing Azure AI Search index: {IndexName}", _options.KnowledgeBaseIndexName);

            // Check if index already exists
            try
            {
                await _indexClient.GetIndexAsync(_options.KnowledgeBaseIndexName, cancellationToken);
                _logger.LogInformation("Index {IndexName} already exists", _options.KnowledgeBaseIndexName);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Index doesn't exist, create it
                _logger.LogInformation("Index {IndexName} not found, creating...", _options.KnowledgeBaseIndexName);
            }

            // Create the search index with vector search configuration
            var searchIndex = new SearchIndex(_options.KnowledgeBaseIndexName)
            {
                Fields =
                {
                    new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                    new SearchableField("title") { IsFilterable = true, IsSortable = true },
                    new SearchableField("content"),
                    new SearchableField("source") { IsFilterable = true },
                    new SimpleField("sourceDeviceId", SearchFieldDataType.String) { IsFilterable = true },
                    new SearchField("tags", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsFilterable = true, IsFacetable = true },
                    new VectorSearchField("contentVector", 1536, "vector-profile"),
                    new SimpleField("usageCount", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
                    new SimpleField("averageRating", SearchFieldDataType.Double) { IsFilterable = true, IsSortable = true },
                    new SimpleField("createdAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                    new SimpleField("updatedAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                    new SimpleField("lastUsedAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true }
                },
                VectorSearch = new VectorSearch
                {
                    Profiles =
                    {
                        new VectorSearchProfile("vector-profile", "vector-config")
                    },
                    Algorithms =
                    {
                        new HnswAlgorithmConfiguration("vector-config")
                    }
                }
            };

            await _indexClient.CreateIndexAsync(searchIndex, cancellationToken);
            _logger.LogInformation("Successfully created index: {IndexName}", _options.KnowledgeBaseIndexName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing search index: {IndexName}", _options.KnowledgeBaseIndexName);
            throw;
        }
    }
}
