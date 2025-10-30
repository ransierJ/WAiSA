using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WAiSA.API.Security.Auditing;

namespace WAiSA.API.Controllers;

/// <summary>
/// Example controller demonstrating audit logging integration
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentController : ControllerBase
{
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IAuditLogger auditLogger,
        ILogger<AgentController> logger)
    {
        _auditLogger = auditLogger;
        _logger = logger;
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteCommand(
        [FromBody] CommandRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        string? result = null;
        string? errorMessage = null;
        string? stackTrace = null;

        try
        {
            // Execute the agent command
            result = await ExecuteAgentCommandAsync(request, cancellationToken);

            return Ok(new { success = true, result });
        }
        catch (UnauthorizedAccessException ex)
        {
            errorMessage = ex.Message;
            stackTrace = ex.StackTrace;

            return StatusCode(403, new { error = "Access denied" });
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            stackTrace = ex.StackTrace;

            _logger.LogError(ex, "Command execution failed: {Command}", request.Command);

            return StatusCode(500, new { error = "Command execution failed" });
        }
        finally
        {
            stopwatch.Stop();

            // Always log the action
            await _auditLogger.LogAgentActionAsync(new AgentActionEvent
            {
                AgentId = request.AgentId,
                SessionId = request.SessionId ?? HttpContext.TraceIdentifier,
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                EventType = EventType.CommandExecution,
                Severity = errorMessage != null ? Severity.High : Severity.Info,
                Command = request.Command,
                Parameters = request.Parameters,
                Result = result,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                SourceIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                AuthenticationMethod = User.Identity?.AuthenticationType ?? "Unknown",
                AuthorizationDecision = errorMessage == null ? "Allowed" : "Denied",
                ErrorMessage = errorMessage,
                StackTrace = stackTrace,
                Metadata = new Dictionary<string, object>
                {
                    ["CorrelationId"] = HttpContext.TraceIdentifier,
                    ["UserAgent"] = Request.Headers.UserAgent.ToString()
                }
            }, cancellationToken);
        }
    }

    [HttpPost("session/start")]
    public async Task<IActionResult> StartSession(
        [FromBody] SessionRequest request,
        CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid().ToString("N");

        await _auditLogger.LogAgentActionAsync(new AgentActionEvent
        {
            AgentId = request.AgentId,
            SessionId = sessionId,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            EventType = EventType.SessionStart,
            Severity = Severity.Info,
            Command = "StartSession",
            Parameters = new Dictionary<string, object>
            {
                ["AgentType"] = request.AgentType,
                ["Configuration"] = request.Configuration ?? new Dictionary<string, object>()
            },
            SourceIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            AuthenticationMethod = User.Identity?.AuthenticationType ?? "Unknown",
            AuthorizationDecision = "Allowed"
        }, cancellationToken);

        return Ok(new { sessionId });
    }

    [HttpPost("session/end")]
    public async Task<IActionResult> EndSession(
        [FromBody] EndSessionRequest request,
        CancellationToken cancellationToken)
    {
        await _auditLogger.LogAgentActionAsync(new AgentActionEvent
        {
            AgentId = request.AgentId,
            SessionId = request.SessionId,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            EventType = EventType.SessionEnd,
            Severity = Severity.Info,
            Command = "EndSession",
            Parameters = new Dictionary<string, object>
            {
                ["Duration"] = request.DurationSeconds,
                ["CommandsExecuted"] = request.CommandsExecuted
            },
            SourceIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            AuthenticationMethod = User.Identity?.AuthenticationType ?? "Unknown",
            AuthorizationDecision = "Allowed"
        }, cancellationToken);

        return Ok(new { success = true });
    }

    [HttpGet("audit/logs")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string? agentId = null,
        [FromQuery] string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var logs = await _auditLogger.QueryLogsAsync(
            new DateTimeOffset(startDate, TimeSpan.Zero),
            new DateTimeOffset(endDate, TimeSpan.Zero),
            agentId,
            userId,
            cancellationToken: cancellationToken);

        return Ok(logs);
    }

    [HttpPost("audit/verify")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> VerifyAuditTrail(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        var logs = await _auditLogger.QueryLogsAsync(
            new DateTimeOffset(startDate, TimeSpan.Zero),
            new DateTimeOffset(endDate, TimeSpan.Zero),
            cancellationToken: cancellationToken);

        var totalLogs = 0;
        var failedVerification = 0;
        var failedLogIds = new List<string>();

        foreach (var log in logs)
        {
            totalLogs++;

            if (!_auditLogger.VerifyIntegrity(log))
            {
                failedVerification++;
                failedLogIds.Add(log.EventId);
            }
        }

        return Ok(new
        {
            totalLogs,
            verifiedLogs = totalLogs - failedVerification,
            failedVerification,
            failedLogIds,
            integrityValid = failedVerification == 0
        });
    }

    private async Task<string> ExecuteAgentCommandAsync(
        CommandRequest request,
        CancellationToken cancellationToken)
    {
        // Simulate command execution
        await Task.Delay(100, cancellationToken);
        return $"Command '{request.Command}' executed successfully";
    }
}

public class CommandRequest
{
    public required string AgentId { get; init; }
    public string? SessionId { get; init; }
    public required string Command { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }
}

public class SessionRequest
{
    public required string AgentId { get; init; }
    public required string AgentType { get; init; }
    public Dictionary<string, object>? Configuration { get; init; }
}

public class EndSessionRequest
{
    public required string AgentId { get; init; }
    public required string SessionId { get; init; }
    public int DurationSeconds { get; init; }
    public int CommandsExecuted { get; init; }
}
