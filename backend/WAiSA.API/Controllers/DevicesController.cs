using Microsoft.AspNetCore.Mvc;
using WAiSA.Core.Interfaces;
using WAiSA.API.Models;

namespace WAiSA.API.Controllers;

/// <summary>
/// Devices API controller for managing Windows device information
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceMemoryService _deviceMemoryService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(
        IDeviceMemoryService deviceMemoryService,
        ILogger<DevicesController> logger)
    {
        _deviceMemoryService = deviceMemoryService;
        _logger = logger;
    }

    /// <summary>
    /// Get all devices
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<DeviceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DeviceDto>>> GetAllDevices(
        CancellationToken cancellationToken)
    {
        var devices = await _deviceMemoryService.GetAllDevicesAsync(cancellationToken);

        return Ok(devices.Select(d => new DeviceDto
        {
            DeviceId = d.DeviceId,
            DeviceName = d.DeviceName,
            ContextSummary = d.ContextSummary,
            TotalInteractions = d.TotalInteractions,
            LastInteractionAt = d.LastInteractionAt,
            LastSummarizedAt = d.LastSummarizedAt,
            Metadata = d.Metadata
        }).ToList());
    }

    /// <summary>
    /// Get device by ID
    /// </summary>
    [HttpGet("{deviceId}")]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceDto>> GetDevice(
        string deviceId,
        CancellationToken cancellationToken)
    {
        var device = await _deviceMemoryService.GetDeviceMemoryAsync(deviceId, cancellationToken);

        if (device == null)
            return NotFound($"Device {deviceId} not found");

        return Ok(new DeviceDto
        {
            DeviceId = device.DeviceId,
            DeviceName = device.DeviceName,
            ContextSummary = device.ContextSummary,
            TotalInteractions = device.TotalInteractions,
            LastInteractionAt = device.LastInteractionAt,
            LastSummarizedAt = device.LastSummarizedAt,
            Metadata = device.Metadata
        });
    }

    /// <summary>
    /// Get device interaction history
    /// </summary>
    [HttpGet("{deviceId}/interactions")]
    [ProducesResponseType(typeof(List<InteractionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<InteractionDto>>> GetInteractions(
        string deviceId,
        [FromQuery] int count = 50,
        CancellationToken cancellationToken = default)
    {
        var interactions = await _deviceMemoryService.GetRecentInteractionsAsync(
            deviceId,
            count,
            cancellationToken);

        return Ok(interactions.Select(i => new InteractionDto
        {
            Id = i.Id,
            UserMessage = i.UserMessage,
            AssistantResponse = i.AssistantResponse,
            Timestamp = i.Timestamp,
            Commands = i.Commands.Select(c => new ExecutedCommandDto
            {
                Command = c.Command,
                Output = c.Output,
                Success = c.Success,
                ErrorMessage = c.ErrorMessage,
                ExecutedAt = c.ExecutedAt
            }).ToList(),
            FeedbackRating = i.FeedbackRating
        }).ToList());
    }

    /// <summary>
    /// Search device interaction history
    /// </summary>
    [HttpGet("{deviceId}/search")]
    [ProducesResponseType(typeof(List<InteractionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<InteractionDto>>> SearchInteractions(
        string deviceId,
        [FromQuery] string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Search query is required");

        var interactions = await _deviceMemoryService.SearchInteractionsAsync(
            deviceId,
            query,
            cancellationToken);

        return Ok(interactions.Select(i => new InteractionDto
        {
            Id = i.Id,
            UserMessage = i.UserMessage,
            AssistantResponse = i.AssistantResponse,
            Timestamp = i.Timestamp,
            Commands = i.Commands.Select(c => new ExecutedCommandDto
            {
                Command = c.Command,
                Output = c.Output,
                Success = c.Success,
                ErrorMessage = c.ErrorMessage,
                ExecutedAt = c.ExecutedAt
            }).ToList(),
            FeedbackRating = i.FeedbackRating
        }).ToList());
    }

    /// <summary>
    /// Trigger manual summarization for a device
    /// </summary>
    [HttpPost("{deviceId}/summarize")]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeviceDto>> TriggerSummarization(
        string deviceId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual summarization triggered for device {DeviceId}", deviceId);

        var device = await _deviceMemoryService.TriggerSummarizationAsync(
            deviceId,
            cancellationToken);

        return Ok(new DeviceDto
        {
            DeviceId = device.DeviceId,
            DeviceName = device.DeviceName,
            ContextSummary = device.ContextSummary,
            TotalInteractions = device.TotalInteractions,
            LastInteractionAt = device.LastInteractionAt,
            LastSummarizedAt = device.LastSummarizedAt,
            Metadata = device.Metadata
        });
    }
}
