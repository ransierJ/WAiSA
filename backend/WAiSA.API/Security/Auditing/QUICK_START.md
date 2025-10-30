# Quick Start Guide - WAiSA AuditLogger

## 1. Add to Program.cs

```csharp
using WAiSA.API.Security.Auditing;

var builder = WebApplication.CreateBuilder(args);

// Add audit logging
builder.Services.AddAuditLogging(builder.Configuration);

// Optional: Application Insights
builder.Services.AddApplicationInsightsTelemetry();
```

## 2. Configure appsettings.json

```json
{
  "AuditLogging": {
    "EnableFileLogging": true,
    "LogDirectory": "/var/log/agent-audit",
    "EnableApplicationInsights": false,
    "LogRetentionDays": 7,
    "CompressedLogRetentionDays": 90
  }
}
```

## 3. Inject and Use

```csharp
public class MyController : ControllerBase
{
    private readonly IAuditLogger _auditLogger;

    public MyController(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    [HttpPost("action")]
    public async Task<IActionResult> DoAction()
    {
        await _auditLogger.LogAgentActionAsync(new AgentActionEvent
        {
            AgentId = "my-agent",
            SessionId = HttpContext.TraceIdentifier,
            UserId = User.Identity?.Name,
            EventType = EventType.CommandExecution,
            Severity = Severity.Info,
            Command = "DoAction",
            SourceIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        return Ok();
    }
}
```

## Event Types

- `CommandExecution` - Agent executed a command
- `DataAccess` - Agent accessed data
- `SecurityViolation` - Security policy violation
- `Authentication` - Auth event
- `Authorization` - Authz decision
- `Error` - Error occurred
- `SessionStart` / `SessionEnd` - Session lifecycle
- `ResourceCreated` / `ResourceModified` / `ResourceDeleted` - Resource changes

## Severity Levels

- `Info` - Normal operation
- `Warning` - Potential issue
- `High` - Requires attention
- `Critical` - Immediate action required

## Auto-Redacted Parameters

Password, Secret, ApiKey, Token, Credential, ConnectionString, Auth, Bearer, AccessToken, RefreshToken, ClientSecret, PrivateKey

## File Locations

- **Linux**: `/var/log/agent-audit/YYYY-MM-DD.log.json`
- **Windows**: `C:\Logs\AgentAudit\YYYY-MM-DD.log.json`

## Query Logs

```csharp
var logs = await _auditLogger.QueryLogsAsync(
    startDate: DateTimeOffset.UtcNow.AddDays(-7),
    endDate: DateTimeOffset.UtcNow,
    agentId: "my-agent",
    userId: "user@example.com",
    eventType: EventType.SecurityViolation
);
```

## Verify Integrity

```csharp
bool isValid = _auditLogger.VerifyIntegrity(logEntry);
```

## Complete Example

```csharp
var stopwatch = Stopwatch.StartNew();
string? result = null;
string? error = null;

try
{
    result = await ExecuteCommand(request);
}
catch (Exception ex)
{
    error = ex.Message;
    throw;
}
finally
{
    await _auditLogger.LogAgentActionAsync(new AgentActionEvent
    {
        AgentId = "agent-001",
        SessionId = request.SessionId,
        UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
        EventType = EventType.CommandExecution,
        Severity = error != null ? Severity.High : Severity.Info,
        Command = request.Command,
        Parameters = request.Parameters,
        Result = result,
        ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
        SourceIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        AuthenticationMethod = "OAuth2",
        AuthorizationDecision = error == null ? "Allowed" : "Denied",
        ErrorMessage = error,
        SubscriptionId = request.SubscriptionId,
        ResourceGroup = request.ResourceGroup
    });
}
```

## Permissions Setup (Linux)

```bash
sudo mkdir -p /var/log/agent-audit
sudo chown www-data:www-data /var/log/agent-audit
sudo chmod 755 /var/log/agent-audit
```

## View Logs

```bash
# View today's logs
cat /var/log/agent-audit/$(date +%Y-%m-%d).log.json | jq

# View compressed logs
zcat /var/log/agent-audit/2025-10-20.log.json.gz | jq

# Search for errors
grep '"severity":"High"' /var/log/agent-audit/*.log.json | jq

# Count events by type
cat /var/log/agent-audit/*.log.json | jq -r '.event_type' | sort | uniq -c
```

## Troubleshooting

**Logs not appearing?**
- Check directory exists and has write permissions
- Check `EnableFileLogging` is `true` in config
- Check application logs for errors

**Disk space issues?**
- Enable compression: `"EnableCompression": true`
- Reduce retention: `"LogRetentionDays": 3`
- Monitor with disk alerts

**Performance issues?**
- Disable Application Insights if not needed
- Disable stack traces: `"IncludeStackTraces": false`

---

For detailed documentation, see [README.md](README.md)
