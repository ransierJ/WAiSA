using System.Security.Cryptography;
using System.Text;
using WAiSA.Core.Entities;
using WAiSA.Core.Interfaces;
using WAiSA.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WAiSA.Infrastructure.Services;

public class AgentService : IAgentService
{
    private readonly WAiSADbContext _context;
    private readonly ILogger<AgentService> _logger;
    private const string INSTALLATION_KEY = "ai-sysadmin-install-2025"; // TODO: Move to configuration

    public AgentService(WAiSADbContext context, ILogger<AgentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(bool Success, string? Message, Guid AgentId, string ApiKey)> RegisterAgentAsync(
        string computerName,
        string installationKey,
        string osVersion,
        string agentVersion)
    {
        try
        {
            // Validate installation key
            if (installationKey != INSTALLATION_KEY)
            {
                _logger.LogWarning("Invalid installation key provided for computer: {ComputerName}", computerName);
                return (false, "Invalid installation key", Guid.Empty, string.Empty);
            }

            // Check if agent already exists for this computer
            var existingAgent = await _context.Agents
                .FirstOrDefaultAsync(a => a.ComputerName == computerName);

            if (existingAgent != null)
            {
                _logger.LogInformation("Agent already registered for computer: {ComputerName}, reactivating", computerName);

                // Reactivate existing agent and generate new API key
                var newApiKey = GenerateApiKey();
                existingAgent.ApiKeyHash = HashApiKey(newApiKey);
                existingAgent.Status = AgentStatus.Online;
                existingAgent.IsEnabled = true;
                existingAgent.Version = agentVersion;
                existingAgent.OsVersion = osVersion;
                existingAgent.LastHeartbeat = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return (true, "Agent reactivated", existingAgent.AgentId, newApiKey);
            }

            // Create new agent
            var agentId = Guid.NewGuid();
            var apiKey = GenerateApiKey();
            var apiKeyHash = HashApiKey(apiKey);

            var agent = new Agent
            {
                AgentId = agentId,
                ComputerName = computerName,
                ApiKeyHash = apiKeyHash,
                Status = AgentStatus.Online,
                InstallDate = DateTime.UtcNow,
                Version = agentVersion,
                OsVersion = osVersion,
                InstallationKey = installationKey,
                IsEnabled = true,
                LastHeartbeat = DateTime.UtcNow
            };

            _context.Agents.Add(agent);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Agent registered successfully: {AgentId} - {ComputerName}", agentId, computerName);

            return (true, "Agent registered successfully", agentId, apiKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering agent for computer: {ComputerName}", computerName);
            return (false, $"Registration error: {ex.Message}", Guid.Empty, string.Empty);
        }
    }

    public async Task<(bool Success, bool HasPendingCommands)> ProcessHeartbeatAsync(
        Guid agentId,
        string status,
        string? systemInfoJson)
    {
        try
        {
            var agent = await _context.Agents.FirstOrDefaultAsync(a => a.AgentId == agentId);

            if (agent == null)
            {
                _logger.LogWarning("Heartbeat received for unknown agent: {AgentId}", agentId);
                return (false, false);
            }

            // Update agent status
            agent.LastHeartbeat = DateTime.UtcNow;
            agent.Status = Enum.TryParse<AgentStatus>(status, out var parsedStatus)
                ? parsedStatus
                : AgentStatus.Online;
            agent.LastSystemInfo = systemInfoJson;

            await _context.SaveChangesAsync();

            // Check for pending commands
            var hasPendingCommands = await _context.CommandQueues
                .AnyAsync(c => c.AgentId == agentId &&
                              (c.Status == CommandStatus.Pending || c.Status == CommandStatus.Approved));

            _logger.LogDebug("Heartbeat processed for agent: {AgentId}, Pending commands: {HasPending}",
                agentId, hasPendingCommands);

            return (true, hasPendingCommands);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing heartbeat for agent: {AgentId}", agentId);
            return (false, false);
        }
    }

    public async Task<bool> ValidateApiKeyAsync(Guid agentId, string apiKey)
    {
        try
        {
            var agent = await _context.Agents.FirstOrDefaultAsync(a => a.AgentId == agentId);

            if (agent == null || !agent.IsEnabled)
            {
                return false;
            }

            var hashedKey = HashApiKey(apiKey);
            return agent.ApiKeyHash == hashedKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating API key for agent: {AgentId}", agentId);
            return false;
        }
    }

    public async Task<List<CommandQueue>> GetPendingCommandsAsync(Guid agentId)
    {
        try
        {
            _logger.LogInformation("🔍 Querying pending commands for agent: {AgentId}", agentId);

            var commands = await _context.CommandQueues
                .Where(c => c.AgentId == agentId &&
                           (c.Status == CommandStatus.Pending || c.Status == CommandStatus.Approved))
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            _logger.LogInformation("📊 Found {Count} pending/approved commands for agent {AgentId}", commands.Count, agentId);

            // Mark commands as executing
            foreach (var command in commands)
            {
                _logger.LogInformation("  📌 Command {CommandId}: Status={Status}, RequiresApproval={RequiresApproval}, Command={Command}",
                    command.CommandId, command.Status, command.RequiresApproval, command.Command);

                if (command.Status == CommandStatus.Pending && !command.RequiresApproval)
                {
                    _logger.LogInformation("  ✅ Marking command {CommandId} as Executing (Pending + NoApprovalRequired)", command.CommandId);
                    command.Status = CommandStatus.Executing;
                    command.StartedAt = DateTime.UtcNow;
                }
                else if (command.Status == CommandStatus.Approved)
                {
                    _logger.LogInformation("  ✅ Marking command {CommandId} as Executing (Approved)", command.CommandId);
                    command.Status = CommandStatus.Executing;
                    command.StartedAt = DateTime.UtcNow;
                }
                else
                {
                    _logger.LogWarning("  ⚠️ Command {CommandId} NOT marked as executing (Status={Status}, RequiresApproval={RequiresApproval})",
                        command.CommandId, command.Status, command.RequiresApproval);
                }
            }

            if (commands.Any())
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("💾 Saved {Count} commands as Executing", commands.Count);
            }

            return commands;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending commands for agent: {AgentId}", agentId);
            return new List<CommandQueue>();
        }
    }

    public async Task<Guid> QueueCommandAsync(
        Guid agentId,
        string command,
        string executionContext,
        int timeoutSeconds = 300,
        bool requiresApproval = false,
        string? initiatedBy = null,
        string? chatSessionId = null)
    {
        try
        {
            var commandId = Guid.NewGuid();
            var status = requiresApproval ? CommandStatus.Pending : CommandStatus.Approved;

            var commandQueue = new CommandQueue
            {
                CommandId = commandId,
                AgentId = agentId,
                Command = command,
                ExecutionContext = executionContext,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                TimeoutSeconds = timeoutSeconds,
                RequiresApproval = requiresApproval,
                InitiatedBy = initiatedBy,
                ChatSessionId = chatSessionId
            };

            _context.CommandQueues.Add(commandQueue);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Command queued: {CommandId} for agent: {AgentId} | Status={Status}, RequiresApproval={RequiresApproval}, Command={Command}",
                commandId, agentId, status, requiresApproval, command);

            // Verification: Query the database to confirm the command was actually saved with correct status
            var savedCommand = await _context.CommandQueues.FirstOrDefaultAsync(c => c.CommandId == commandId);
            if (savedCommand != null)
            {
                _logger.LogInformation("🔍 VERIFY: Command saved to DB - CommandId={CommandId}, Status={Status}, RequiresApproval={RequiresApproval}, AgentId={AgentId}",
                    savedCommand.CommandId, savedCommand.Status, savedCommand.RequiresApproval, savedCommand.AgentId);
            }
            else
            {
                _logger.LogError("❌ VERIFY FAILED: Command {CommandId} NOT found in database after SaveChanges!", commandId);
            }

            return commandId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing command for agent: {AgentId}", agentId);
            throw;
        }
    }

    public async Task<bool> SubmitCommandResultAsync(
        Guid commandId,
        bool success,
        string output,
        string? error,
        DateTime startTime,
        DateTime endTime,
        double executionTimeSeconds)
    {
        try
        {
            var command = await _context.CommandQueues.FirstOrDefaultAsync(c => c.CommandId == commandId);

            if (command == null)
            {
                _logger.LogWarning("Command not found: {CommandId}", commandId);
                return false;
            }

            command.Success = success;
            command.Output = output;
            command.Error = error;
            command.StartedAt = startTime;
            command.CompletedAt = endTime;
            command.ExecutionTimeSeconds = executionTimeSeconds;
            command.Status = success ? CommandStatus.Completed : CommandStatus.Failed;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Command result submitted: {CommandId}, Success: {Success}",
                commandId, success);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting command result: {CommandId}", commandId);
            return false;
        }
    }

    public async Task<Agent?> GetAgentAsync(Guid agentId)
    {
        return await _context.Agents.FirstOrDefaultAsync(a => a.AgentId == agentId);
    }

    public async Task<List<Agent>> GetAllAgentsAsync()
    {
        return await _context.Agents
            .OrderByDescending(a => a.LastHeartbeat)
            .ToListAsync();
    }

    public async Task<List<Agent>> GetOnlineAgentsAsync()
    {
        var onlineThreshold = DateTime.UtcNow.AddMinutes(-2); // Consider offline if no heartbeat in 2 minutes

        return await _context.Agents
            .Where(a => a.IsEnabled &&
                       a.LastHeartbeat.HasValue &&
                       a.LastHeartbeat.Value >= onlineThreshold)
            .OrderBy(a => a.ComputerName)
            .ToListAsync();
    }

    public async Task<bool> ApproveCommandAsync(Guid commandId, string approvedBy)
    {
        try
        {
            var command = await _context.CommandQueues.FirstOrDefaultAsync(c => c.CommandId == commandId);

            if (command == null || command.Status != CommandStatus.Pending)
            {
                return false;
            }

            command.Approved = true;
            command.ApprovedBy = approvedBy;
            command.ApprovedAt = DateTime.UtcNow;
            command.Status = CommandStatus.Approved;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Command approved: {CommandId} by {ApprovedBy}", commandId, approvedBy);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving command: {CommandId}", commandId);
            return false;
        }
    }

    public async Task<bool> CancelCommandAsync(Guid commandId)
    {
        try
        {
            var command = await _context.CommandQueues.FirstOrDefaultAsync(c => c.CommandId == commandId);

            if (command == null ||
                (command.Status != CommandStatus.Pending && command.Status != CommandStatus.Approved))
            {
                return false;
            }

            command.Status = CommandStatus.Cancelled;
            command.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Command cancelled: {CommandId}", commandId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling command: {CommandId}", commandId);
            return false;
        }
    }

    public async Task<List<CommandQueue>> GetPendingApprovalsAsync()
    {
        try
        {
            return await _context.CommandQueues
                .Include(c => c.Agent)
                .Where(c => c.RequiresApproval &&
                           c.Status == CommandStatus.Pending &&
                           c.Approved != true)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending approvals");
            return new List<CommandQueue>();
        }
    }

    public async Task<List<CommandQueue>> GetPendingApprovalsForAgentAsync(Guid agentId)
    {
        try
        {
            return await _context.CommandQueues
                .Include(c => c.Agent)
                .Where(c => c.AgentId == agentId &&
                           c.RequiresApproval &&
                           c.Status == CommandStatus.Pending &&
                           c.Approved != true)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending approvals for agent: {AgentId}", agentId);
            return new List<CommandQueue>();
        }
    }

    // Helper methods
    private string GenerateApiKey()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes);
    }

    private string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public async Task<CommandQueue?> GetCommandAsync(Guid commandId)
    {
        return await _context.CommandQueues
            .FirstOrDefaultAsync(c => c.CommandId == commandId);
    }

    public async Task<CommandQueue?> WaitForCommandCompletionAsync(Guid commandId, int timeoutSeconds = 30, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        _logger.LogInformation("⏳ Waiting for command {CommandId} to complete (timeout: {Timeout}s)", commandId, timeoutSeconds);

        while ((DateTime.UtcNow - startTime) < timeout && !cancellationToken.IsCancellationRequested)
        {
            // Use AsNoTracking to force fresh database query and avoid EF caching
            var command = await _context.CommandQueues
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CommandId == commandId, cancellationToken);

            if (command != null)
            {
                _logger.LogDebug("📊 Command {CommandId} status check: Status={Status}, Elapsed={Elapsed}s",
                    commandId, command.Status, (DateTime.UtcNow - startTime).TotalSeconds);

                if (command.Status == CommandStatus.Completed || command.Status == CommandStatus.Failed || command.Status == CommandStatus.TimedOut)
                {
                    _logger.LogInformation("✅ Command {CommandId} completed with status: {Status}", commandId, command.Status);
                    return command;
                }
            }

            // Wait 500ms before checking again
            await Task.Delay(500, cancellationToken);
        }

        _logger.LogWarning("⚠️ Command {CommandId} wait timed out after {Timeout}s", commandId, timeoutSeconds);

        // Return current state if timeout reached (use AsNoTracking for final check too)
        return await _context.CommandQueues
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CommandId == commandId, cancellationToken);
    }

    public async Task<List<CommandQueue>> GetAllCommandsForAgentAsync(Guid agentId, int limit = 50)
    {
        try
        {
            _logger.LogInformation("🔍 DIAGNOSTIC: Querying ALL commands for agent: {AgentId} (limit: {Limit})", agentId, limit);

            var commands = await _context.CommandQueues
                .Where(c => c.AgentId == agentId)
                .OrderByDescending(c => c.CreatedAt)
                .Take(limit)
                .ToListAsync();

            _logger.LogInformation("📊 DIAGNOSTIC: Found {Count} total commands for agent {AgentId}", commands.Count, agentId);

            foreach (var cmd in commands)
            {
                _logger.LogInformation("  📌 DIAGNOSTIC: CommandId={CommandId}, Status={Status}, RequiresApproval={RequiresApproval}, Command={Command}, CreatedAt={CreatedAt}",
                    cmd.CommandId, cmd.Status, cmd.RequiresApproval, cmd.Command, cmd.CreatedAt);
            }

            return commands;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all commands for agent: {AgentId}", agentId);
            return new List<CommandQueue>();
        }
    }
}
