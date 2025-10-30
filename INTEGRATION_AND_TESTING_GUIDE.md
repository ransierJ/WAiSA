# Phase 1 Security Components - Integration & Testing Guide

## Quick Start Integration

### 1. Verify Dependencies

The security components are already integrated into `Program.cs`. Verify the services are registered:

```bash
# Check Program.cs contains security service registrations
grep -A 30 "AI Agent Security Services" backend/WAiSA.API/Program.cs
```

Expected output should show 5 service registrations:
- `ILateralMovementGuard`
- `IStagingManager`
- Input Validation extensions
- Command Filtering extensions
- Audit Logging extensions

### 2. Build and Verify Compilation

```bash
cd backend/WAiSA.API
dotnet build
```

Expected: **Build succeeded. 0 Error(s)**

If you encounter errors, verify:
- YamlDotNet NuGet package is installed
- All security files are included in the project
- `ai-agent-guardrails-enhanced.yml` exists at repository root

### 3. Run Unit Tests

```bash
# Run all security component tests
dotnet test --filter "FullyQualifiedName~Security"

# Or run specific component tests
dotnet test --filter "FullyQualifiedName~LateralMovementGuardTests"
dotnet test --filter "FullyQualifiedName~StagingManagerTests"
dotnet test --filter "FullyQualifiedName~InputValidatorTests"
dotnet test --filter "FullyQualifiedName~CommandFilteringEngineTests"
dotnet test --filter "FullyQualifiedName~AuditLoggerTests"
```

Expected: **All tests pass**

---

## Using Security Components in Your Code

### Example 1: Validating Agent Commands (Lateral Movement Guard)

```csharp
using WAiSA.API.Security.Guards;

public class AgentController : ControllerBase
{
    private readonly ILateralMovementGuard _lateralMovementGuard;

    public AgentController(ILateralMovementGuard lateralMovementGuard)
    {
        _lateralMovementGuard = lateralMovementGuard;
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteCommand([FromBody] CommandRequest request)
    {
        // Step 1: Validate for lateral movement
        var validation = await _lateralMovementGuard.ValidateCommandAsync(
            request.Command,
            HttpContext.RequestAborted
        );

        if (!validation.IsAllowed)
        {
            return BadRequest(new
            {
                error = "Command blocked",
                reason = validation.BlockedReason,
                violationType = validation.ViolationType?.ToString()
            });
        }

        // Step 2: Continue with command execution
        // ... your execution logic

        return Ok();
    }
}
```

### Example 2: Creating Staging Environment

```csharp
using WAiSA.API.Security.Staging;

public class ScriptExecutionService
{
    private readonly IStagingManager _stagingManager;

    public async Task<string> ExecuteAgentScript(
        string agentId,
        string sessionId,
        string scriptContent)
    {
        // Step 1: Create isolated staging environment
        var environment = await _stagingManager.CreateStagingEnvironmentAsync(
            agentId,
            sessionId
        );

        try
        {
            // Step 2: Validate and stage the script
            var validation = await _stagingManager.ValidateAndStageScriptAsync(
                environment,
                scriptContent,
                "agent-script.ps1"
            );

            if (!validation.IsValid)
            {
                throw new SecurityException(
                    $"Script validation failed: {validation.ErrorMessage}"
                );
            }

            // Step 3: Execute from staging directory
            var result = await ExecuteScriptFromPath(validation.StagedScriptPath);

            return result;
        }
        finally
        {
            // Step 4: Always cleanup (even on errors)
            await _stagingManager.CleanupStagingEnvironmentAsync(environment);
        }
    }
}
```

### Example 3: Input Validation

```csharp
using WAiSA.API.Security.Validation;

public class AgentInputService
{
    private readonly IInputValidator _inputValidator;

    public ValidationResult ValidateUserInput(
        string command,
        Dictionary<string, string> parameters)
    {
        // Step 1: Validate the command
        var commandValidation = _inputValidator.ValidateCommand(command, parameters);

        if (!commandValidation.IsValid)
        {
            _logger.LogWarning(
                "Command validation failed: {Violations}",
                string.Join(", ", commandValidation.Violations)
            );
            return commandValidation;
        }

        // Step 2: Check for injection patterns
        var injectionCheck = _inputValidator.CheckForInjectionPatterns(command);

        if (!injectionCheck.IsValid)
        {
            _logger.LogWarning(
                "Injection pattern detected: {Pattern}",
                injectionCheck.DetectedPattern
            );
            return injectionCheck;
        }

        // Step 3: Sanitize parameters
        var sanitizedParams = _inputValidator.SanitizeParameters(parameters);

        // Step 4: Check for path traversal
        var pathTraversalViolations = _inputValidator.CheckForPathTraversal(sanitizedParams);

        if (pathTraversalViolations.Any())
        {
            _logger.LogWarning(
                "Path traversal detected in {Count} parameters",
                pathTraversalViolations.Count
            );
            return ValidationResult.Fail("Path traversal attempt detected");
        }

        return ValidationResult.Success();
    }
}
```

### Example 4: Command Filtering with Context

```csharp
using WAiSA.API.Security.Filtering;

public class AgentCommandService
{
    private readonly ICommandFilteringEngine _filteringEngine;
    private readonly IAuditLogger _auditLogger;

    public async Task<CommandFilterResult> FilterAndExecuteCommand(
        string agentId,
        string userId,
        string command,
        Dictionary<string, string> parameters)
    {
        // Step 1: Create agent context
        var context = new AgentContext
        {
            AgentId = agentId,
            Role = AgentRole.ReadOnly,  // Start conservative
            Environment = WAiSA.API.Security.Filtering.Environment.Production,
            SessionId = HttpContext.Session.Id,
            UserId = userId
        };

        // Step 2: Filter the command through all layers
        var filterResult = await _filteringEngine.FilterCommandAsync(
            context,
            command,
            parameters,
            CancellationToken.None
        );

        // Step 3: Handle filtering decision
        if (!filterResult.IsAllowed)
        {
            // Log the blocked command
            await _auditLogger.LogAgentActionAsync(new AgentActionEvent
            {
                AgentId = agentId,
                UserId = userId,
                Action = "command_blocked",
                Command = command,
                BlockedReason = filterResult.BlockedReason,
                Severity = Severity.High
            });

            // Check if approval is required
            if (filterResult.RequiresApproval)
            {
                return filterResult with
                {
                    Message = "Command requires manual approval"
                };
            }

            return filterResult;
        }

        // Step 4: Execute approved command
        // ... your execution logic

        return filterResult;
    }
}
```

### Example 5: Comprehensive Audit Logging

```csharp
using WAiSA.API.Security.Auditing;

public class AgentExecutionService
{
    private readonly IAuditLogger _auditLogger;

    public async Task ExecuteWithAuditing(
        string agentId,
        string userId,
        string command)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Execute command
            var result = await ExecuteCommandAsync(command);

            // Log successful execution
            await _auditLogger.LogAgentActionAsync(new AgentActionEvent
            {
                AgentId = agentId,
                UserId = userId,
                Action = "command_execution",
                Command = command,
                Severity = Severity.Info,
                EventData = new EventData
                {
                    Command = command,
                    ResultStatus = "success",
                    ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds
                },
                SecurityContext = new SecurityContext
                {
                    SourceIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    AuthenticationMethod = "managed_identity",
                    AuthorizationDecision = "allow"
                }
            });
        }
        catch (Exception ex)
        {
            // Log failed execution
            await _auditLogger.LogAgentActionAsync(new AgentActionEvent
            {
                AgentId = agentId,
                UserId = userId,
                Action = "command_execution_failed",
                Command = command,
                Severity = Severity.High,
                EventData = new EventData
                {
                    Command = command,
                    ResultStatus = "failed",
                    ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    ErrorMessage = ex.Message
                }
            });

            throw;
        }
    }
}
```

---

## Testing Scenarios

### Scenario 1: Block Lateral Movement

**Test**: Verify remote execution commands are blocked

```bash
# Start the API
cd backend/WAiSA.API
dotnet run
```

**Test Request** (using curl or Postman):

```bash
curl -X POST http://localhost:5000/api/agent/execute \
  -H "Content-Type: application/json" \
  -d '{
    "command": "Enter-PSSession -ComputerName prod-server-01"
  }'
```

**Expected Response**:
```json
{
  "error": "Command blocked",
  "reason": "Remote execution cmdlet 'Enter-PSSession' is not allowed",
  "violationType": "RemoteCmdlet"
}
```

**Verify Audit Log**:
```bash
# Linux
tail -f /var/log/agent-audit/$(date +%Y-%m-%d).log.json | jq 'select(.event_type == "lateral_movement_attempt")'

# Windows
type C:\Logs\AgentAudit\2025-10-28.log.json | findstr "lateral_movement"
```

### Scenario 2: Allow Safe Information Gathering

**Test**: Verify read-only commands are allowed

```bash
curl -X POST http://localhost:5000/api/agent/execute \
  -H "Content-Type: application/json" \
  -d '{
    "command": "Get-Service -DisplayName W3SVC"
  }'
```

**Expected Response**:
```json
{
  "status": "success",
  "result": "..."
}
```

**Verify Audit Log** shows `event_type: "command_execution"` with `severity: "info"`

### Scenario 3: Detect Command Injection

**Test**: Verify injection patterns are caught

```bash
curl -X POST http://localhost:5000/api/agent/execute \
  -H "Content-Type: application/json" \
  -d '{
    "command": "Get-Process; rm -rf /var/data"
  }'
```

**Expected Response**:
```json
{
  "error": "Dangerous pattern detected",
  "pattern": "Command chaining with destructive operation",
  "violations": ["command_injection", "dangerous_operation"]
}
```

### Scenario 4: Staging Directory Security

**Test**: Verify path traversal is blocked

```csharp
// Unit test or integration test
[Fact]
public async Task StagingManager_Should_Block_Path_Traversal()
{
    var stagingManager = GetService<IStagingManager>();

    var environment = await stagingManager.CreateStagingEnvironmentAsync(
        "test-agent",
        "test-session"
    );

    var validation = await stagingManager.ValidateAndStageScriptAsync(
        environment,
        "Get-Content ../../../etc/passwd",
        "../evil.ps1"  // Path traversal attempt
    );

    Assert.False(validation.IsValid);
    Assert.Contains("path traversal", validation.ErrorMessage.ToLower());
}
```

### Scenario 5: Rate Limiting

**Test**: Verify rate limits are enforced

```bash
# Send 101 requests in 60 seconds
for i in {1..101}; do
  curl -X POST http://localhost:5000/api/agent/execute \
    -H "Content-Type: application/json" \
    -d '{"command": "Get-Date"}' &
done
wait
```

**Expected**: First 100 succeed, 101st returns:
```json
{
  "error": "Rate limit exceeded",
  "limit": "100 requests per minute",
  "retry_after": 42
}
```

---

## Configuration Customization

### Development Environment

Edit `ai-agent-guardrails-enhanced.yml`:

```yaml
agent_security:
  # More permissive for development
  information_gathering:
    rate_limiting:
      requests_per_minute: 1000  # Higher limit

  autonomy_tiers:
    tier_1_read_only:
      default_for_new_agents: true  # Start at Tier 1 instead of Tier 0
```

### Production Environment

```yaml
agent_security:
  # Strict security for production
  lateral_movement:
    quarantine_agent: true  # Immediately quarantine on violation

  command_filtering:
    strictness: "paranoid"  # Maximum security

  autonomy_tiers:
    tier_0_manual:
      default_for_new_agents: true  # All commands require approval
```

### Custom Allowed Commands

Add organization-specific cmdlets:

```yaml
command_filtering:
  allowed_cmdlets:
    custom_organization:
      - "Get-CompanyAsset"
      - "Get-CompanyUser"
      - "Get-CompanyInventory"
```

---

## Troubleshooting

### Issue: "YAML configuration file not found"

**Symptom**:
```
FileNotFoundException: Could not find file '/.../ai-agent-guardrails-enhanced.yml'
```

**Solution**:
```bash
# Verify file exists
ls -la ai-agent-guardrails-enhanced.yml

# Check Program.cs path calculation
# Path should be relative to backend/WAiSA.API/bin/Debug/net8.0/
cd backend/WAiSA.API
dotnet run
```

The path in `Program.cs` uses:
```csharp
Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "ai-agent-guardrails-enhanced.yml")
```

This resolves to repository root when running from `backend/WAiSA.API/bin/Debug/net8.0/`.

### Issue: Audit logs not being created

**Symptom**: No log files in `/var/log/agent-audit/`

**Solution**:
```bash
# Create directory with proper permissions
sudo mkdir -p /var/log/agent-audit
sudo chown $USER:$USER /var/log/agent-audit
sudo chmod 755 /var/log/agent-audit

# Verify appsettings.Security.json configuration
cat backend/WAiSA.API/appsettings.Security.json | grep -A 5 "AuditLogging"

# Check Application Insights is configured (if enabled)
cat backend/WAiSA.API/appsettings.json | grep "ApplicationInsights"
```

### Issue: All commands being blocked

**Symptom**: Even safe commands like `Get-Service` are blocked

**Solution**:
```bash
# Check agent role/tier
# Ensure agent context is set correctly:

var context = new AgentContext
{
    Role = AgentRole.ReadOnly,  // Not Manual (Tier 0)
    Environment = Environment.Development  // Or Production
};

# Verify whitelist in configuration
grep -A 20 "allowed_cmdlets:" ai-agent-guardrails-enhanced.yml
```

### Issue: Staging directory permission errors

**Symptom**:
```
UnauthorizedAccessException: Access to the path is denied
```

**Solution**:
```bash
# Linux: Verify base path exists
sudo mkdir -p /var/agent-staging
sudo chown $USER:$USER /var/agent-staging

# Windows: Verify base path exists
mkdir C:\AgentStaging

# Check appsettings.Security.json
cat backend/WAiSA.API/appsettings.Security.json | grep -A 5 "Staging"
```

---

## Viewing Audit Logs

### Local File Logs (NDJSON format)

```bash
# View today's logs
tail -f /var/log/agent-audit/$(date +%Y-%m-%d).log.json | jq .

# Search for high-severity events
cat /var/log/agent-audit/*.log.json | jq 'select(.severity == "high" or .severity == "critical")'

# Count events by type
cat /var/log/agent-audit/*.log.json | jq -r '.event_type' | sort | uniq -c

# Find specific agent's actions
cat /var/log/agent-audit/*.log.json | jq 'select(.agent_id == "agent-123")'

# Events in last hour
cat /var/log/agent-audit/*.log.json | jq 'select(.timestamp > "'$(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%S)'")'
```

### Application Insights (if enabled)

Use Azure Portal or KQL queries:

```kql
// High-severity security events
customEvents
| where timestamp > ago(24h)
| where name == "AgentSecurityEvent"
| where customDimensions.severity in ("high", "critical")
| project timestamp,
          event_type = customDimensions.event_type,
          agent_id = customDimensions.agent_id,
          reason = customDimensions.blocked_reason
| order by timestamp desc

// Rate limit violations over time
customEvents
| where name == "RateLimitExceeded"
| summarize count() by bin(timestamp, 5m), tostring(customDimensions.agent_id)
| render timechart

// Lateral movement attempts
customEvents
| where name == "LateralMovementAttempt"
| project timestamp,
          agent_id = customDimensions.agent_id,
          command = customDimensions.command,
          source_ip = customDimensions.source_ip
```

---

## Performance Benchmarks

Expected performance for security pipeline (all 5 components):

- **Lateral Movement Guard**: < 1ms
- **Input Validation**: < 2ms (40+ patterns)
- **Command Filtering**: < 5ms (6 layers)
- **Staging Validation**: < 10ms (includes file I/O)
- **Audit Logging**: < 5ms (async write)

**Total Pipeline**: < 10ms for typical command

Test with:
```csharp
var stopwatch = Stopwatch.StartNew();

// Run through complete pipeline
await ValidateCommand(command);

stopwatch.Stop();
Console.WriteLine($"Validation time: {stopwatch.ElapsedMilliseconds}ms");
```

---

## Integration Checklist

- [ ] Build succeeds: `dotnet build`
- [ ] All tests pass: `dotnet test`
- [ ] YAML configuration loads correctly
- [ ] Audit log directory exists and is writable
- [ ] Application Insights configured (if using)
- [ ] Lateral movement blocking tested
- [ ] Input validation tested with injection attempts
- [ ] Staging directory security tested
- [ ] Rate limiting tested
- [ ] Audit logs being written and readable
- [ ] Security components injected via DI
- [ ] Error handling works as expected
- [ ] Performance benchmarks meet requirements

---

## Next Steps After Testing

Once Phase 1 testing is complete:

1. **Baseline Learning** (7-30 days)
   - Let system learn normal patterns
   - Review false positives
   - Tune sensitivity settings

2. **Phase 2 Implementation** (Week 2)
   - Advanced rate limiting with Redis
   - Circuit breaker patterns
   - Resource quotas and monitoring

3. **Phase 3 Implementation** (Week 3-4)
   - ML-based anomaly detection
   - SIEM integration UI
   - Approval workflow dashboards

---

**Document Version**: 1.0
**Last Updated**: 2025-10-28
**Phase**: 1 - Integration & Testing
