using WAiSA.API.Models;
using WAiSA.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace WAiSA.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(IAgentService agentService, ILogger<AgentsController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new agent
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AgentRegistrationResponseDto), 200)]
    public async Task<IActionResult> Register([FromBody] AgentRegistrationRequestDto request)
    {
        _logger.LogInformation("Agent registration request from: {ComputerName}", request.ComputerName);

        var (success, message, agentId, apiKey) = await _agentService.RegisterAgentAsync(
            request.ComputerName,
            request.InstallationKey,
            request.OsVersion,
            request.AgentVersion);

        var response = new AgentRegistrationResponseDto
        {
            Success = success,
            Message = message,
            AgentId = agentId,
            ApiKey = apiKey
        };

        if (!success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Process heartbeat from agent
    /// </summary>
    [HttpPost("heartbeat")]
    [ProducesResponseType(typeof(HeartbeatResponseDto), 200)]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequestDto request)
    {
        // Validate API key
        var apiKey = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        if (string.IsNullOrEmpty(apiKey))
        {
            return Unauthorized(new HeartbeatResponseDto
            {
                Success = false,
                Message = "API key required"
            });
        }

        var isValid = await _agentService.ValidateApiKeyAsync(request.AgentId, apiKey);
        if (!isValid)
        {
            _logger.LogWarning("Invalid API key for agent: {AgentId}", request.AgentId);
            return Unauthorized(new HeartbeatResponseDto
            {
                Success = false,
                Message = "Invalid API key"
            });
        }

        // Process heartbeat
        var systemInfoJson = request.SystemInfo != null
            ? JsonSerializer.Serialize(request.SystemInfo)
            : null;

        var (success, hasPendingCommands) = await _agentService.ProcessHeartbeatAsync(
            request.AgentId,
            request.Status,
            systemInfoJson);

        var response = new HeartbeatResponseDto
        {
            Success = success,
            HasPendingCommands = hasPendingCommands
        };

        return Ok(response);
    }

    /// <summary>
    /// Get pending commands for an agent
    /// </summary>
    [HttpGet("{agentId}/commands/pending")]
    [ProducesResponseType(typeof(PendingCommandsResponseDto), 200)]
    public async Task<IActionResult> GetPendingCommands(Guid agentId)
    {
        _logger.LogInformation("🔍 Agent {AgentId} polling for pending commands", agentId);

        // Validate API key
        var apiKey = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("❌ No API key provided for agent {AgentId}", agentId);
            return Unauthorized();
        }

        var isValid = await _agentService.ValidateApiKeyAsync(agentId, apiKey);
        if (!isValid)
        {
            _logger.LogWarning("❌ Invalid API key for agent: {AgentId}", agentId);
            return Unauthorized();
        }

        _logger.LogInformation("✅ Agent {AgentId} authenticated successfully", agentId);

        var commands = await _agentService.GetPendingCommandsAsync(agentId);

        _logger.LogInformation("📋 Found {Count} pending commands for agent {AgentId}", commands.Count, agentId);

        if (commands.Any())
        {
            foreach (var cmd in commands)
            {
                _logger.LogInformation("  📌 Command {CommandId}: {Command}", cmd.CommandId, cmd.Command);
            }
        }

        var response = new PendingCommandsResponseDto
        {
            Commands = commands.Select(c => new CommandRequestDto
            {
                CommandId = c.CommandId,
                Command = c.Command,
                ExecutionContext = c.ExecutionContext ?? string.Empty,
                TimeoutSeconds = c.TimeoutSeconds
            }).ToList()
        };

        return Ok(response);
    }

    /// <summary>
    /// Submit command execution result
    /// </summary>
    [HttpPost("{agentId}/commands/result")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> SubmitCommandResult(Guid agentId, [FromBody] CommandResponseDto request)
    {
        // Validate API key
        var apiKey = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        if (string.IsNullOrEmpty(apiKey))
        {
            return Unauthorized();
        }

        var isValid = await _agentService.ValidateApiKeyAsync(agentId, apiKey);
        if (!isValid)
        {
            _logger.LogWarning("Invalid API key for agent: {AgentId}", agentId);
            return Unauthorized();
        }

        var success = await _agentService.SubmitCommandResultAsync(
            request.CommandId,
            request.Success,
            request.Output,
            request.Error,
            request.StartTime,
            request.EndTime,
            request.ExecutionTimeSeconds);

        if (!success)
        {
            return BadRequest(new { message = "Failed to submit command result" });
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Get all agents
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<AgentDto>), 200)]
    public async Task<IActionResult> GetAllAgents()
    {
        var agents = await _agentService.GetAllAgentsAsync();

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var response = agents.Select(a => new AgentDto
        {
            AgentId = a.AgentId,
            ComputerName = a.ComputerName,
            Status = a.Status.ToString(),
            LastHeartbeat = a.LastHeartbeat,
            InstallDate = a.InstallDate,
            Version = a.Version,
            OsVersion = a.OsVersion,
            LastSystemInfo = !string.IsNullOrEmpty(a.LastSystemInfo)
                ? TryDeserializeSystemInfo(a.LastSystemInfo, jsonOptions)
                : null,
            IsEnabled = a.IsEnabled
        }).ToList();

        return Ok(response);
    }

    private SystemInformationDto? TryDeserializeSystemInfo(string json, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Deserialize<SystemInformationDto>(json, options);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize system info JSON: {Json}", json);
            return null;
        }
    }

    /// <summary>
    /// Get online agents
    /// </summary>
    [HttpGet("online")]
    [ProducesResponseType(typeof(List<AgentDto>), 200)]
    public async Task<IActionResult> GetOnlineAgents()
    {
        var agents = await _agentService.GetOnlineAgentsAsync();

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var response = agents.Select(a => new AgentDto
        {
            AgentId = a.AgentId,
            ComputerName = a.ComputerName,
            Status = a.Status.ToString(),
            LastHeartbeat = a.LastHeartbeat,
            InstallDate = a.InstallDate,
            Version = a.Version,
            OsVersion = a.OsVersion,
            LastSystemInfo = !string.IsNullOrEmpty(a.LastSystemInfo)
                ? TryDeserializeSystemInfo(a.LastSystemInfo, jsonOptions)
                : null,
            IsEnabled = a.IsEnabled
        }).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Get specific agent
    /// </summary>
    [HttpGet("{agentId}")]
    [ProducesResponseType(typeof(AgentDto), 200)]
    public async Task<IActionResult> GetAgent(Guid agentId)
    {
        var agent = await _agentService.GetAgentAsync(agentId);

        if (agent == null)
        {
            return NotFound(new { message = "Agent not found" });
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var response = new AgentDto
        {
            AgentId = agent.AgentId,
            ComputerName = agent.ComputerName,
            Status = agent.Status.ToString(),
            LastHeartbeat = agent.LastHeartbeat,
            InstallDate = agent.InstallDate,
            Version = agent.Version,
            OsVersion = agent.OsVersion,
            LastSystemInfo = !string.IsNullOrEmpty(agent.LastSystemInfo)
                ? TryDeserializeSystemInfo(agent.LastSystemInfo, jsonOptions)
                : null,
            IsEnabled = agent.IsEnabled
        };

        return Ok(response);
    }

    /// <summary>
    /// Queue a command for execution (for admin/chat use)
    /// </summary>
    [HttpPost("{agentId}/commands/queue")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> QueueCommand(Guid agentId, [FromBody] QueueCommandRequestDto request)
    {
        var commandId = await _agentService.QueueCommandAsync(
            agentId,
            request.Command,
            request.ExecutionContext ?? "Manual",
            request.TimeoutSeconds,
            request.RequiresApproval,
            request.InitiatedBy,
            request.ChatSessionId);

        return Ok(new { commandId, message = "Command queued successfully" });
    }

    /// <summary>
    /// Approve a pending command
    /// </summary>
    [HttpPost("commands/{commandId}/approve")]
    public async Task<IActionResult> ApproveCommand(Guid commandId, [FromBody] ApproveCommandRequestDto request)
    {
        var success = await _agentService.ApproveCommandAsync(commandId, request.ApprovedBy);

        if (!success)
        {
            return BadRequest(new { message = "Failed to approve command" });
        }

        return Ok(new { message = "Command approved" });
    }

    /// <summary>
    /// Cancel a pending command
    /// </summary>
    [HttpPost("commands/{commandId}/cancel")]
    public async Task<IActionResult> CancelCommand(Guid commandId)
    {
        var success = await _agentService.CancelCommandAsync(commandId);

        if (!success)
        {
            return BadRequest(new { message = "Failed to cancel command" });
        }

        return Ok(new { message = "Command cancelled" });
    }

    /// <summary>
    /// Get all pending approval commands
    /// </summary>
    [HttpGet("commands/pending-approvals")]
    [ProducesResponseType(typeof(List<PendingApprovalDto>), 200)]
    public async Task<IActionResult> GetPendingApprovals()
    {
        var commands = await _agentService.GetPendingApprovalsAsync();

        var response = commands.Select(c => new PendingApprovalDto
        {
            CommandId = c.CommandId,
            AgentId = c.AgentId,
            AgentName = c.Agent?.ComputerName ?? "Unknown",
            Command = c.Command,
            ExecutionContext = c.ExecutionContext ?? string.Empty,
            CreatedAt = c.CreatedAt,
            TimeoutSeconds = c.TimeoutSeconds,
            InitiatedBy = c.InitiatedBy ?? "Unknown",
            ChatSessionId = c.ChatSessionId
        }).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Get pending approval commands for a specific agent
    /// </summary>
    [HttpGet("{agentId}/commands/pending-approvals")]
    [ProducesResponseType(typeof(List<PendingApprovalDto>), 200)]
    public async Task<IActionResult> GetPendingApprovalsForAgent(Guid agentId)
    {
        var commands = await _agentService.GetPendingApprovalsForAgentAsync(agentId);

        var response = commands.Select(c => new PendingApprovalDto
        {
            CommandId = c.CommandId,
            AgentId = c.AgentId,
            AgentName = c.Agent?.ComputerName ?? "Unknown",
            Command = c.Command,
            ExecutionContext = c.ExecutionContext ?? string.Empty,
            CreatedAt = c.CreatedAt,
            TimeoutSeconds = c.TimeoutSeconds,
            InitiatedBy = c.InitiatedBy ?? "Unknown",
            ChatSessionId = c.ChatSessionId
        }).ToList();

        return Ok(response);
    }

    /// <summary>
    /// DIAGNOSTIC: Get ALL commands for an agent (for debugging)
    /// </summary>
    [HttpGet("{agentId}/commands/diagnostic")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetAllCommandsDiagnostic(Guid agentId, [FromQuery] int limit = 50)
    {
        _logger.LogInformation("🔍 DIAGNOSTIC: Fetching ALL commands for agent: {AgentId}", agentId);

        var allCommands = await _agentService.GetAllCommandsForAgentAsync(agentId, limit);

        _logger.LogInformation("📊 DIAGNOSTIC: Returning {Count} total commands", allCommands.Count);

        return Ok(new {
            agentId = agentId,
            timestamp = DateTime.UtcNow,
            totalCount = allCommands.Count,
            limit = limit,
            commands = allCommands.Select(c => new {
                commandId = c.CommandId,
                command = c.Command,
                status = c.Status.ToString(),
                requiresApproval = c.RequiresApproval,
                approved = c.Approved,
                approvedBy = c.ApprovedBy,
                createdAt = c.CreatedAt,
                startedAt = c.StartedAt,
                completedAt = c.CompletedAt,
                executionContext = c.ExecutionContext,
                success = c.Success,
                output = c.Output != null ? c.Output.Substring(0, Math.Min(200, c.Output.Length)) : null,
                error = c.Error
            }).ToList()
        });
    }
}

// DTOs
public class PendingApprovalDto
{
    public Guid CommandId { get; set; }
    public Guid AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string ExecutionContext { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int TimeoutSeconds { get; set; }
    public string InitiatedBy { get; set; } = string.Empty;
    public string? ChatSessionId { get; set; }
}

// Additional DTOs for controller actions
public class QueueCommandRequestDto
{
    public string Command { get; set; } = string.Empty;
    public string? ExecutionContext { get; set; }
    public int TimeoutSeconds { get; set; } = 300;
    public bool RequiresApproval { get; set; }
    public string? InitiatedBy { get; set; }
    public string? ChatSessionId { get; set; }
}

public class ApproveCommandRequestDto
{
    public string ApprovedBy { get; set; } = string.Empty;
}
