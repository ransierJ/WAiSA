using WAiSA.Shared.Models;

namespace WAiSA.Core.Interfaces;

/// <summary>
/// Device Memory Service for managing per-device persistent memory with AI summarization
/// </summary>
public interface IDeviceMemoryService
{
    /// <summary>
    /// Get device memory by device ID
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device memory or null if not found</returns>
    Task<DeviceMemory?> GetDeviceMemoryAsync(
        string deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create or update device memory
    /// </summary>
    /// <param name="deviceMemory">Device memory to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveDeviceMemoryAsync(
        DeviceMemory deviceMemory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Record a new interaction for a device
    /// </summary>
    /// <param name="interaction">Interaction to record</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecordInteractionAsync(
        Interaction interaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent interactions for a device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="count">Number of interactions to retrieve (default 20)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of recent interactions</returns>
    Task<List<Interaction>> GetRecentInteractionsAsync(
        string deviceId,
        int count = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trigger context summarization for a device
    /// Called automatically after every 20 interactions
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated device memory with new context summary</returns>
    Task<DeviceMemory> TriggerSummarizationAsync(
        string deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all devices with their memory summaries
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all device memories</returns>
    Task<List<DeviceMemory>> GetAllDevicesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search interaction history for a device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="searchQuery">Search query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching interactions</returns>
    Task<List<Interaction>> SearchInteractionsAsync(
        string deviceId,
        string searchQuery,
        CancellationToken cancellationToken = default);
}
