using WAiSA.Core.Entities;

namespace WAiSA.Core.Interfaces;

/// <summary>
/// Service for managing Windows agents and command execution
/// </summary>
public interface IAgentService
{
    /// <summary>
    /// Register a new agent with the system
    /// </summary>
    Task<(bool Success, string? Message, Guid AgentId, string ApiKey)> RegisterAgentAsync(
        string computerName,
        string installationKey,
        string osVersion,
        string agentVersion);

    /// <summary>
    /// Process heartbeat from agent and update status
    /// </summary>
    Task<(bool Success, bool HasPendingCommands)> ProcessHeartbeatAsync(
        Guid agentId,
        string status,
        string? systemInfoJson);

    /// <summary>
    /// Validate agent API key
    /// </summary>
    Task<bool> ValidateApiKeyAsync(Guid agentId, string apiKey);

    /// <summary>
    /// Get pending commands for an agent
    /// </summary>
    Task<List<CommandQueue>> GetPendingCommandsAsync(Guid agentId);

    /// <summary>
    /// Queue a command for execution on an agent
    /// </summary>
    Task<Guid> QueueCommandAsync(
        Guid agentId,
        string command,
        string executionContext,
        int timeoutSeconds = 300,
        bool requiresApproval = false,
        string? initiatedBy = null,
        string? chatSessionId = null);

    /// <summary>
    /// Submit command execution result
    /// </summary>
    Task<bool> SubmitCommandResultAsync(
        Guid commandId,
        bool success,
        string output,
        string? error,
        DateTime startTime,
        DateTime endTime,
        double executionTimeSeconds);

    /// <summary>
    /// Get agent by ID
    /// </summary>
    Task<Agent?> GetAgentAsync(Guid agentId);

    /// <summary>
    /// Get all agents
    /// </summary>
    Task<List<Agent>> GetAllAgentsAsync();

    /// <summary>
    /// Get online agents
    /// </summary>
    Task<List<Agent>> GetOnlineAgentsAsync();

    /// <summary>
    /// Mark command as approved
    /// </summary>
    Task<bool> ApproveCommandAsync(Guid commandId, string approvedBy);

    /// <summary>
    /// Cancel a pending command
    /// </summary>
    Task<bool> CancelCommandAsync(Guid commandId);

    /// <summary>
    /// Get all pending approval commands
    /// </summary>
    Task<List<CommandQueue>> GetPendingApprovalsAsync();

    /// <summary>
    /// Get pending approval commands for a specific agent
    /// </summary>
    Task<List<CommandQueue>> GetPendingApprovalsForAgentAsync(Guid agentId);

    /// <summary>
    /// Get command by ID
    /// </summary>
    Task<CommandQueue?> GetCommandAsync(Guid commandId);

    /// <summary>
    /// Wait for command to complete execution (with timeout)
    /// </summary>
    Task<CommandQueue?> WaitForCommandCompletionAsync(Guid commandId, int timeoutSeconds = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get ALL commands for an agent (for diagnostic purposes)
    /// </summary>
    Task<List<CommandQueue>> GetAllCommandsForAgentAsync(Guid agentId, int limit = 50);
}
