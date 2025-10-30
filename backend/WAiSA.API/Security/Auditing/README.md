# WAiSA Audit Logging System

Comprehensive audit logging system for AI agent actions in ASP.NET Core 8.0.

## Features

- **Structured Logging**: JSON-formatted audit logs with complete event context
- **Multiple Destinations**: File-based logging and optional Application Insights integration
- **Security**: Automatic PII/credential sanitization and integrity hashing (SHA256)
- **Performance**: Parallel logging, thread-safe operations, and async processing
- **Compliance**: Tamper-evident logs with integrity verification
- **Retention**: Automatic log rotation, compression (gzip), and cleanup
- **Query Support**: Date-range queries with filtering by agent, user, and event type

## Installation

### 1. Add to Program.cs

```csharp
using WAiSA.API.Security.Auditing;

var builder = WebApplication.CreateBuilder(args);

// Add audit logging
builder.Services.AddAuditLogging(builder.Configuration);

// Optional: Add Application Insights if needed
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();
```

### 2. Configure appsettings.json

```json
{
  "AuditLogging": {
    "EnableFileLogging": true,
    "LogDirectory": "/var/log/agent-audit",
    "EnableApplicationInsights": false,
    "LogRetentionDays": 7,
    "CompressedLogRetentionDays": 90,
    "MaxLogFileSizeMb": 100,
    "EnableCompression": true,
    "IncludeStackTraces": true,
    "BufferSize": 0
  }
}
```

### 3. For Windows Development

```json
{
  "AuditLogging": {
    "LogDirectory": "C:\\Logs\\AgentAudit"
  }
}
```

## Usage

### Basic Logging

```csharp
public class AgentController : ControllerBase
{
    private readonly IAuditLogger _auditLogger;

    public AgentController(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteCommand(
        [FromBody] CommandRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        string? result = null;
        string? errorMessage = null;

        try
        {
            result = await ExecuteAgentCommand(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            // Log the action
            await _auditLogger.LogAgentActionAsync(new AgentActionEvent
            {
                AgentId = request.AgentId,
                SessionId = request.SessionId,
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                EventType = EventType.CommandExecution,
                Severity = errorMessage != null ? Severity.High : Severity.Info,
                Command = request.Command,
                Parameters = request.Parameters,
                Result = result,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                SourceIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                AuthenticationMethod = "OAuth2",
                AuthorizationDecision = "Allowed",
                ErrorMessage = errorMessage
            }, cancellationToken);
        }
    }
}
```

### Security Violation Logging

```csharp
public class SecurityMiddleware
{
    private readonly IAuditLogger _auditLogger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!await IsAuthorized(context))
        {
            await _auditLogger.LogAgentActionAsync(new AgentActionEvent
            {
                AgentId = "system",
                SessionId = context.TraceIdentifier,
                UserId = context.User.Identity?.Name,
                EventType = EventType.SecurityViolation,
                Severity = Severity.Critical,
                Command = $"{context.Request.Method} {context.Request.Path}",
                SourceIpAddress = context.Connection.RemoteIpAddress?.ToString(),
                AuthenticationMethod = context.User.Identity?.AuthenticationType,
                AuthorizationDecision = "Denied",
                ErrorMessage = "Unauthorized access attempt"
            });

            context.Response.StatusCode = 403;
            return;
        }

        await _next(context);
    }
}
```

### Azure Resource Operations

```csharp
public async Task CreateVirtualMachine(VmRequest request)
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
        var vm = await _azureClient.CreateVmAsync(request);

        await _auditLogger.LogAgentActionAsync(new AgentActionEvent
        {
            AgentId = "azure-provisioning-agent",
            SessionId = Activity.Current?.Id ?? Guid.NewGuid().ToString(),
            UserId = request.RequestedBy,
            EventType = EventType.ResourceCreated,
            Severity = Severity.Info,
            Command = "CreateVirtualMachine",
            Parameters = new Dictionary<string, object>
            {
                ["vmName"] = request.VmName,
                ["size"] = request.Size,
                ["region"] = request.Region
            },
            Result = $"VM created: {vm.Id}",
            ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            SubscriptionId = request.SubscriptionId,
            ResourceGroup = request.ResourceGroup,
            ResourceId = vm.Id
        });
    }
    catch (Exception ex)
    {
        stopwatch.Stop();

        await _auditLogger.LogAgentActionAsync(new AgentActionEvent
        {
            AgentId = "azure-provisioning-agent",
            SessionId = Activity.Current?.Id ?? Guid.NewGuid().ToString(),
            UserId = request.RequestedBy,
            EventType = EventType.Error,
            Severity = Severity.High,
            Command = "CreateVirtualMachine",
            Parameters = new Dictionary<string, object>
            {
                ["vmName"] = request.VmName
            },
            ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            ErrorMessage = ex.Message,
            StackTrace = ex.StackTrace,
            SubscriptionId = request.SubscriptionId,
            ResourceGroup = request.ResourceGroup
        });

        throw;
    }
}
```

### Querying Audit Logs

```csharp
public class AuditReportService
{
    private readonly IAuditLogger _auditLogger;

    public async Task<IEnumerable<AuditLogEntry>> GetAgentActivityReport(
        string agentId,
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        var logs = await _auditLogger.QueryLogsAsync(
            startDate,
            endDate,
            agentId: agentId);

        return logs.OrderByDescending(l => l.Timestamp);
    }

    public async Task<IEnumerable<AuditLogEntry>> GetSecurityViolations(
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        var logs = await _auditLogger.QueryLogsAsync(
            startDate,
            endDate,
            eventType: EventType.SecurityViolation);

        return logs.Where(l => l.Severity == "High" || l.Severity == "Critical");
    }
}
```

### Verifying Log Integrity

```csharp
public class LogVerificationService
{
    private readonly IAuditLogger _auditLogger;

    public async Task<bool> VerifyAuditTrail(
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        var logs = await _auditLogger.QueryLogsAsync(startDate, endDate);
        var failedCount = 0;

        foreach (var log in logs)
        {
            if (!_auditLogger.VerifyIntegrity(log))
            {
                failedCount++;
                _logger.LogWarning(
                    "Integrity verification failed for log entry: {EventId}",
                    log.EventId);
            }
        }

        return failedCount == 0;
    }
}
```

## Log Format

Each log entry is a single-line JSON object:

```json
{
  "timestamp": "2025-10-28T10:30:45.123Z",
  "event_id": "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6",
  "agent_id": "azure-ops-agent-01",
  "session_id": "sess_12345",
  "user_id": "user@example.com",
  "event_type": "CommandExecution",
  "severity": "Info",
  "event_data": {
    "command": "CreateResourceGroup",
    "parameters": {
      "name": "rg-production",
      "location": "eastus",
      "apiKey": "***REDACTED***"
    },
    "result": "Resource group created successfully",
    "execution_time_ms": 1234
  },
  "security_context": {
    "source_ip": "192.168.1.100",
    "auth_method": "OAuth2",
    "authorization_decision": "Allowed"
  },
  "resource_context": {
    "subscription_id": "sub-12345",
    "resource_group": "rg-production",
    "resource_id": "/subscriptions/sub-12345/resourceGroups/rg-production"
  },
  "integrity_hash": "a1b2c3d4e5f6..."
}
```

## Sanitization

The following parameter names are automatically redacted:
- password, secret, apikey, api_key
- token, credential, connectionstring
- auth, authorization, bearer
- accesstoken, refreshtoken, clientsecret
- privatekey

Values are also sanitized if they match sensitive data patterns (JWT tokens, Base64 credentials, etc.).

## File Structure

```
/var/log/agent-audit/
├── 2025-10-28.log.json              # Current day
├── 2025-10-27.log.json              # Yesterday
├── 2025-10-26.120000.log.json.gz    # Rotated and compressed
├── 2025-10-25.log.json.gz           # Compressed
└── 2025-10-24.log.json.gz
```

## Performance Considerations

1. **Singleton Service**: Registered as singleton for optimal performance
2. **Parallel Logging**: File and Application Insights logging happen in parallel
3. **Thread-Safe**: Uses SemaphoreSlim for safe concurrent file access
4. **Async Operations**: All I/O operations are asynchronous
5. **Non-Blocking**: Audit failures don't break application flow

## Application Insights Integration

When Application Insights is enabled, audit logs are sent as custom events with:
- Event name: `AgentAudit.{EventType}`
- Custom properties for all audit fields
- Metrics for execution time and output size
- Correlated with other telemetry via session/operation IDs

## Compliance

- **SOC 2**: Comprehensive audit trail with integrity verification
- **HIPAA**: PII sanitization and secure storage
- **ISO 27001**: Access logging and security monitoring
- **PCI DSS**: Sensitive data redaction

## Troubleshooting

### Logs not appearing
1. Check directory permissions: `sudo chmod 755 /var/log/agent-audit`
2. Verify configuration in appsettings.json
3. Check application logs for errors

### Performance issues
1. Increase buffer size for batch writing
2. Disable Application Insights if not needed
3. Reduce log retention days
4. Disable stack trace logging

### Disk space concerns
1. Enable compression
2. Reduce retention periods
3. Monitor with disk space alerts

## Security Best Practices

1. **File Permissions**: Ensure log directory has restricted permissions (750 or 755)
2. **Network Isolation**: Keep logs on isolated storage if possible
3. **Regular Audits**: Periodically verify log integrity
4. **Access Control**: Limit who can read audit logs
5. **Backup**: Include audit logs in backup strategy

## License

Copyright (c) 2025 WAiSA. All rights reserved.
