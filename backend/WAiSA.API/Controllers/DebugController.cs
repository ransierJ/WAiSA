using Microsoft.AspNetCore.Mvc;
using WAiSA.Core.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using WAiSA.Shared.Configuration;

namespace WAiSA.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private readonly IDeviceMemoryService _deviceMemoryService;
    private readonly ILogger<DebugController> _logger;
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosDbOptions _cosmosOptions;

    public DebugController(
        IDeviceMemoryService deviceMemoryService,
        ILogger<DebugController> logger,
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> cosmosOptions)
    {
        _deviceMemoryService = deviceMemoryService;
        _logger = logger;
        _cosmosClient = cosmosClient;
        _cosmosOptions = cosmosOptions.Value;
    }

    [HttpGet("interactions/{deviceId}")]
    public async Task<IActionResult> GetInteractions(string deviceId)
    {
        try
        {
            _logger.LogWarning("DEBUG_ENDPOINT: Fetching interactions for DeviceId: '{DeviceId}'", deviceId);
            _logger.LogWarning("DEBUG_ENDPOINT: DeviceId Length: {Length}, DeviceId Bytes: {Bytes}",
                deviceId.Length,
                string.Join(",", System.Text.Encoding.UTF8.GetBytes(deviceId).Select(b => b.ToString())));

            var interactions = await _deviceMemoryService.GetRecentInteractionsAsync(deviceId, 50);

            var result = new
            {
                DeviceId = deviceId,
                DeviceIdLength = deviceId.Length,
                DeviceIdHex = BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(deviceId)),
                TotalCount = interactions.Count,
                QueryParameters = new
                {
                    RequestedDeviceId = deviceId,
                    QueryCount = 50
                },
                Interactions = interactions.Select(i => new
                {
                    i.Id,
                    DeviceId = i.DeviceId,
                    DeviceIdLength = i.DeviceId?.Length,
                    DeviceIdMatches = i.DeviceId == deviceId,
                    DeviceIdHex = i.DeviceId != null ? BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(i.DeviceId)) : null,
                    i.ConversationId,
                    i.UserMessage,
                    AssistantResponsePreview = i.AssistantResponse?.Substring(0, Math.Min(100, i.AssistantResponse?.Length ?? 0)),
                    i.Timestamp
                }).ToList()
            };

            _logger.LogWarning(
                "DEBUG_ENDPOINT: Retrieved {Count} interactions for DeviceId: '{DeviceId}'",
                interactions.Count,
                deviceId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG_ENDPOINT: Error fetching interactions for DeviceId: '{DeviceId}'", deviceId);
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpGet("cosmos-raw/{deviceId}")]
    public async Task<IActionResult> QueryCosmosDirectly(string deviceId)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_cosmosOptions.DatabaseName, "interaction-history");

            // Try multiple query variations to see what works
            var queries = new Dictionary<string, string>
            {
                ["Exact Match"] = $"SELECT * FROM c WHERE c.DeviceId = '{deviceId}'",
                ["With Partition"] = $"SELECT * FROM c WHERE c.DeviceId = @deviceId",
                ["All Documents"] = "SELECT TOP 10 c.id, c.DeviceId, c.ConversationId, c.Timestamp FROM c ORDER BY c.Timestamp DESC",
                ["Count by DeviceId"] = $"SELECT VALUE COUNT(1) FROM c WHERE c.DeviceId = '{deviceId}'"
            };

            var results = new Dictionary<string, object>();

            foreach (var kvp in queries)
            {
                try
                {
                    var queryDef = new QueryDefinition(kvp.Value);
                    if (kvp.Key == "With Partition")
                    {
                        queryDef = queryDef.WithParameter("@deviceId", deviceId);
                    }

                    var iterator = container.GetItemQueryIterator<dynamic>(queryDef);
                    var items = new List<dynamic>();

                    while (iterator.HasMoreResults)
                    {
                        var response = await iterator.ReadNextAsync();
                        items.AddRange(response);
                    }

                    results[kvp.Key] = new {
                        Query = kvp.Value,
                        ResultCount = items.Count,
                        Results = items
                    };
                }
                catch (Exception ex)
                {
                    results[kvp.Key] = new { error = ex.Message };
                }
            }

            return Ok(new
            {
                DeviceId = deviceId,
                Queries = results
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }
}
