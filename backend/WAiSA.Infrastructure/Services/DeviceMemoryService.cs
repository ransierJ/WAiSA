using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using WAiSA.Core.Interfaces;
using WAiSA.Shared.Configuration;
using WAiSA.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// Device Memory Service implementation using Azure Cosmos DB
/// </summary>
public class DeviceMemoryService : IDeviceMemoryService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _deviceMemoryContainer;
    private readonly Container _interactionHistoryContainer;
    private readonly CosmosDbOptions _options;
    private readonly IAIOrchestrationService _aiService;
    private readonly ILogger<DeviceMemoryService> _logger;

    public DeviceMemoryService(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        IAIOrchestrationService aiService,
        ILogger<DeviceMemoryService> logger)
    {
        _cosmosClient = cosmosClient;
        _options = options.Value;
        _aiService = aiService;
        _logger = logger;

        // Get database and containers from injected Cosmos client
        var database = _cosmosClient.GetDatabase(_options.DatabaseName);
        _deviceMemoryContainer = database.GetContainer(_options.DeviceMemoryContainer);
        _interactionHistoryContainer = database.GetContainer(_options.InteractionHistoryContainer);
    }

    /// <summary>
    /// Get device memory by device ID
    /// </summary>
    public async Task<DeviceMemory?> GetDeviceMemoryAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _deviceMemoryContainer.GetItemLinqQueryable<DeviceMemory>()
                .Where(m => m.DeviceId == deviceId)
                .ToFeedIterator();

            if (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                return response.FirstOrDefault();
            }

            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving device memory for {DeviceId}", deviceId);
            return null;
        }
    }

    /// <summary>
    /// Create or update device memory
    /// </summary>
    public async Task SaveDeviceMemoryAsync(
        DeviceMemory deviceMemory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            deviceMemory.UpdatedAt = DateTime.UtcNow;

            await _deviceMemoryContainer.UpsertItemAsync(
                deviceMemory,
                new PartitionKey(deviceMemory.DeviceId),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Saved device memory for {DeviceId}. Total interactions: {Count}",
                deviceMemory.DeviceId,
                deviceMemory.TotalInteractions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving device memory for {DeviceId}", deviceMemory.DeviceId);
            throw;
        }
    }

    /// <summary>
    /// Record a new interaction for a device
    /// </summary>
    public async Task RecordInteractionAsync(
        Interaction interaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // DEBUG: Log Container instance hash to verify singleton behavior
            _logger.LogWarning("CONTAINER_DEBUG: RecordInteractionAsync using Container instance {ContainerHash}",
                _interactionHistoryContainer.GetHashCode());

            // Save interaction to history (with 30-day TTL) and capture response
            var writeResponse = await _interactionHistoryContainer.CreateItemAsync(
                interaction,
                new PartitionKey(interaction.DeviceId),
                cancellationToken: cancellationToken);

            // WRITE_VERIFICATION_DEBUG: Log write response details AND exact parameter values
            _logger.LogWarning(
                "WRITE_VERIFICATION_DEBUG: Write completed - StatusCode: {StatusCode}, RequestCharge: {RU}, " +
                "SessionToken: {SessionToken}, ActivityId: {ActivityId}, " +
                "InteractionId: {InteractionId}, DeviceId: '{DeviceId}', ConversationId: '{ConversationId}', " +
                "PartitionKey: '{PartitionKey}'",
                writeResponse.StatusCode,
                writeResponse.RequestCharge,
                writeResponse.Headers.Session?.Substring(0, Math.Min(50, writeResponse.Headers.Session?.Length ?? 0)) ?? "null",
                writeResponse.ActivityId,
                interaction.Id,
                interaction.DeviceId,
                interaction.ConversationId,
                interaction.DeviceId);

            // POINT_READ_TEST: Immediately verify document exists with direct point read
            try
            {
                var pointReadResponse = await _interactionHistoryContainer.ReadItemAsync<Interaction>(
                    interaction.Id,
                    new PartitionKey(interaction.DeviceId),
                    cancellationToken: cancellationToken);

                _logger.LogWarning(
                    "POINT_READ_TEST: ✅ Successfully read document immediately after write - " +
                    "StatusCode: {StatusCode}, InteractionId: {InteractionId}, DeviceId: {DeviceId}",
                    pointReadResponse.StatusCode,
                    pointReadResponse.Resource.Id,
                    pointReadResponse.Resource.DeviceId);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(
                    "POINT_READ_TEST: ⛔ FAILED to read document immediately after write - " +
                    "StatusCode: {StatusCode}, InteractionId: {InteractionId}, DeviceId: {DeviceId}, Error: {Error}",
                    ex.StatusCode,
                    interaction.Id,
                    interaction.DeviceId,
                    ex.Message);
            }

            // Update device memory
            var deviceMemory = await GetDeviceMemoryAsync(interaction.DeviceId, cancellationToken);
            if (deviceMemory == null)
            {
                // Create new device memory
                deviceMemory = new DeviceMemory
                {
                    DeviceId = interaction.DeviceId,
                    DeviceName = interaction.DeviceId, // Will be updated by client
                    ContextSummary = string.Empty,
                    TotalInteractions = 0,
                    InteractionsSinceLastSummary = 0
                };
            }

            deviceMemory.TotalInteractions++;
            deviceMemory.InteractionsSinceLastSummary++;
            deviceMemory.LastInteractionAt = DateTime.UtcNow;

            await SaveDeviceMemoryAsync(deviceMemory, cancellationToken);

            // Check if summarization is needed
            if (deviceMemory.InteractionsSinceLastSummary >= _options.SummarizationThreshold)
            {
                _logger.LogInformation(
                    "Triggering summarization for device {DeviceId} after {Count} interactions",
                    interaction.DeviceId,
                    deviceMemory.InteractionsSinceLastSummary);

                // Trigger summarization asynchronously (don't await)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await TriggerSummarizationAsync(interaction.DeviceId, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in background summarization for {DeviceId}", interaction.DeviceId);
                    }
                });
            }

            _logger.LogInformation(
                "Recorded interaction for device {DeviceId}. Total: {Total}, Since summary: {Since}",
                interaction.DeviceId,
                deviceMemory.TotalInteractions,
                deviceMemory.InteractionsSinceLastSummary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording interaction for {DeviceId}", interaction.DeviceId);
            throw;
        }
    }

    /// <summary>
    /// Get recent interactions for a device
    /// </summary>
    public async Task<List<Interaction>> GetRecentInteractionsAsync(
        string deviceId,
        int count = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // DEBUG: Log Container instance hash to verify singleton behavior
            _logger.LogWarning("CONTAINER_DEBUG: GetRecentInteractionsAsync using Container instance {ContainerHash}",
                _interactionHistoryContainer.GetHashCode());

            // Use raw SQL query to bypass LINQ-to-CosmosDB query provider issues
            // The LINQ provider was failing to find recently written documents
            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.DeviceId = @deviceId ORDER BY c.Timestamp DESC OFFSET 0 LIMIT @count")
                .WithParameter("@deviceId", deviceId)
                .WithParameter("@count", count);

            // Use QueryRequestOptions with explicit PartitionKey to ensure session consistency
            var queryOptions = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(deviceId)
            };

            // QUERY_PARAMS_DEBUG: Log exact parameters being used in query
            _logger.LogWarning(
                "QUERY_PARAMS_DEBUG: Executing query with DeviceId: '{DeviceId}', Count: {Count}, " +
                "PartitionKey: '{PartitionKey}', SQL: {SQL}",
                deviceId,
                count,
                deviceId,
                queryDefinition.QueryText);

            var query = _interactionHistoryContainer.GetItemQueryIterator<Interaction>(
                queryDefinition,
                requestOptions: queryOptions);

            var interactions = new List<Interaction>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                interactions.AddRange(response);

                // QUERY_RESULTS_DEBUG: Log each batch of results
                _logger.LogWarning(
                    "QUERY_RESULTS_DEBUG: Query batch returned {Count} interactions, " +
                    "RequestCharge: {RU}, ActivityId: {ActivityId}",
                    response.Count,
                    response.RequestCharge,
                    response.ActivityId);
            }

            // QUERY_FINAL_DEBUG: Log final results with details
            _logger.LogWarning(
                "QUERY_FINAL_DEBUG: Query returned {TotalCount} total interactions for DeviceId: '{DeviceId}'",
                interactions.Count,
                deviceId);

            if (interactions.Any())
            {
                foreach (var interaction in interactions)
                {
                    _logger.LogWarning(
                        "QUERY_FINAL_DEBUG: Found interaction - Id: {Id}, DeviceId: '{DeviceId}', " +
                        "ConversationId: '{ConversationId}', Timestamp: {Timestamp}",
                        interaction.Id,
                        interaction.DeviceId,
                        interaction.ConversationId,
                        interaction.Timestamp);
                }
            }

            return interactions.OrderBy(i => i.Timestamp).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent interactions for {DeviceId}", deviceId);
            return new List<Interaction>();
        }
    }

    /// <summary>
    /// Trigger context summarization for a device
    /// </summary>
    public async Task<DeviceMemory> TriggerSummarizationAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting summarization for device {DeviceId}", deviceId);

            // Get recent interactions
            var recentInteractions = await GetRecentInteractionsAsync(
                deviceId,
                _options.SummarizationThreshold,
                cancellationToken);

            if (!recentInteractions.Any())
            {
                _logger.LogWarning("No interactions found for summarization for device {DeviceId}", deviceId);
                var memory = await GetDeviceMemoryAsync(deviceId, cancellationToken);
                return memory ?? new DeviceMemory { DeviceId = deviceId };
            }

            // Generate summary using AI
            var summary = await _aiService.SummarizeContextAsync(
                recentInteractions,
                _options.MaxSummaryTokens,
                cancellationToken);

            // Update device memory
            var deviceMemory = await GetDeviceMemoryAsync(deviceId, cancellationToken);
            if (deviceMemory == null)
            {
                deviceMemory = new DeviceMemory
                {
                    DeviceId = deviceId,
                    DeviceName = deviceId
                };
            }

            deviceMemory.ContextSummary = summary;
            deviceMemory.SummaryTokenCount = EstimateTokenCount(summary);
            deviceMemory.InteractionsSinceLastSummary = 0;
            deviceMemory.LastSummarizedAt = DateTime.UtcNow;

            await SaveDeviceMemoryAsync(deviceMemory, cancellationToken);

            _logger.LogInformation(
                "Summarization completed for device {DeviceId}. Summary tokens: {Tokens}",
                deviceId,
                deviceMemory.SummaryTokenCount);

            return deviceMemory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during summarization for {DeviceId}", deviceId);
            throw;
        }
    }

    /// <summary>
    /// Get all devices with their memory summaries
    /// </summary>
    public async Task<List<DeviceMemory>> GetAllDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _deviceMemoryContainer.GetItemLinqQueryable<DeviceMemory>()
                .OrderByDescending(m => m.LastInteractionAt)
                .ToFeedIterator();

            var devices = new List<DeviceMemory>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                devices.AddRange(response);
            }

            return devices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all devices");
            return new List<DeviceMemory>();
        }
    }

    /// <summary>
    /// Search interaction history for a device
    /// </summary>
    public async Task<List<Interaction>> SearchInteractionsAsync(
        string deviceId,
        string searchQuery,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use raw SQL query with CONTAINS for text search to bypass LINQ provider issues
            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.DeviceId = @deviceId AND " +
                "(CONTAINS(c.UserMessage, @searchQuery) OR CONTAINS(c.AssistantResponse, @searchQuery)) " +
                "ORDER BY c.Timestamp DESC")
                .WithParameter("@deviceId", deviceId)
                .WithParameter("@searchQuery", searchQuery);

            var queryOptions = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(deviceId)
            };

            var query = _interactionHistoryContainer.GetItemQueryIterator<Interaction>(
                queryDefinition,
                requestOptions: queryOptions);

            var interactions = new List<Interaction>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                interactions.AddRange(response);
            }

            return interactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching interactions for {DeviceId}", deviceId);
            return new List<Interaction>();
        }
    }

    /// <summary>
    /// Estimate token count for text (rough approximation)
    /// </summary>
    private int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Rough estimate: 1 token ≈ 4 characters
        return text.Length / 4;
    }
}
