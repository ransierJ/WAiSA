# CommandFilteringEngine - Quick Reference

## 🚀 Quick Start

### 1. Install Dependencies
```bash
dotnet add package YamlDotNet --version 13.7.1
```

### 2. Register Services (Program.cs)
```csharp
builder.Services.AddCommandFiltering(
    builder.Configuration,
    configPath: "ai-agent-guardrails-enhanced.yml");
```

### 3. Inject and Use
```csharp
public class AgentController : ControllerBase
{
    private readonly ICommandFilteringEngine _filteringEngine;

    public AgentController(ICommandFilteringEngine filteringEngine)
    {
        _filteringEngine = filteringEngine;
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteCommand([FromBody] CommandRequest request)
    {
        var context = new AgentContext
        {
            AgentId = request.AgentId,
            SessionId = HttpContext.Session.Id,
            Role = AgentRole.ReadOnly,
            Environment = AgentEnvironment.Production
        };

        var result = await _filteringEngine.FilterCommandAsync(
            context,
            request.Command,
            request.Parameters);

        if (!result.IsAllowed)
        {
            return BadRequest(new { result.Reason, result.Message });
        }

        if (result.RequiresApproval)
        {
            return Accepted(); // Queue for approval
        }

        // Execute command
        return Ok();
    }
}
```

## 🎯 Core Interfaces

### ICommandFilteringEngine
```csharp
Task<CommandFilterResult> FilterCommandAsync(
    AgentContext context,
    string command,
    Dictionary<string, string>? parameters = null,
    CancellationToken cancellationToken = default);

bool IsCommandWhitelisted(string command, AgentRole role, AgentEnvironment environment);

(bool IsBlacklisted, string? MatchedPattern) IsCommandBlacklisted(string command);

Task<ParameterValidationResult> ValidateParametersAsync(
    string command,
    Dictionary<string, string> parameters,
    AllowedParameters allowedConfig,
    CancellationToken cancellationToken = default);
```

## 📊 Agent Roles

| Role | Level | Permissions | Use Case |
|------|-------|-------------|----------|
| **Manual** | 0 | Suggest only | Initial deployment, high-risk |
| **ReadOnly** | 1 | Get-*, Test-*, Measure-* | Information gathering |
| **LimitedWrite** | 2 | ReadOnly + service mgmt | Service management |
| **Supervised** | 3 | Config changes, no destructive | Maintenance |
| **FullAutonomy** | 4 | Full (isolated only) | Lab/testing only |

## 🌍 Environments

| Environment | Restrictions | Allowed Roles |
|-------------|--------------|---------------|
| **Development** | Relaxed | All roles |
| **Staging** | Moderate | Manual → Supervised |
| **Production** | Strict | Manual → Supervised |
| **Isolated** | Full audit | All roles including FullAutonomy |

## 🛡️ Validation Layers

```
Command → Syntax → Blacklist → Whitelist → Semantic → Context → Rate-limit → Result
```

1. **Syntax**: Length, quotes, brackets, control chars
2. **Blacklist**: Dangerous patterns (injection, escalation)
3. **Whitelist**: Role-based permissions
4. **Semantic**: Malicious intent detection
5. **Context**: Session anomalies, role constraints
6. **Rate-limit**: Token bucket (100/min, 1000/hour)

## 📝 Common Patterns

### Create Agent Context
```csharp
// Manual mode
var context = AgentContext.CreateManual("agent-1", "session-1", AgentEnvironment.Production);

// ReadOnly mode
var context = AgentContext.CreateReadOnly("agent-1", "session-1", AgentEnvironment.Development);

// Custom
var context = new AgentContext
{
    AgentId = "agent-001",
    SessionId = Guid.NewGuid().ToString(),
    Role = AgentRole.LimitedWrite,
    Environment = AgentEnvironment.Production,
    UserId = User.Identity?.Name,
    TenantId = "tenant-123",
    SourceAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
};
```

### Check Filter Result
```csharp
var result = await _filteringEngine.FilterCommandAsync(context, command);

if (!result.IsAllowed)
{
    _logger.LogWarning("Command blocked: {Reason} - {Message}", result.Reason, result.Message);

    switch (result.Reason)
    {
        case FilterReason.Blacklisted:
            // Log security incident
            break;
        case FilterReason.NotWhitelisted:
            // Request whitelist expansion
            break;
        case FilterReason.RateLimitExceeded:
            // Return 429 Too Many Requests
            break;
    }

    return BadRequest(new
    {
        error = "Command blocked",
        reason = result.Reason.ToString(),
        message = result.Message,
        decisionId = result.DecisionId
    });
}

if (result.RequiresApproval)
{
    await _approvalService.RequestApprovalAsync(result);
    return Accepted(new { approvalId = result.DecisionId });
}
```

### Validate Command Only
```csharp
// Just check whitelist
bool isWhitelisted = _filteringEngine.IsCommandWhitelisted(
    "Get-Process",
    AgentRole.ReadOnly,
    AgentEnvironment.Production);

// Just check blacklist
var (isBlacklisted, pattern) = _filteringEngine.IsCommandBlacklisted(command);
if (isBlacklisted)
{
    _logger.LogWarning("Blacklist match: {Pattern}", pattern);
}
```

## ⚙️ Configuration Examples

### Minimal Configuration
```csharp
builder.Services.AddCommandFiltering(config =>
{
    config.Enabled = true;
    config.Strategy = "whitelist-first";
    config.ValidationLayers = new List<string> { "blacklist", "whitelist" };
});
```

### Programmatic Configuration
```csharp
var config = new CommandFilteringConfigBuilder()
    .EnableFiltering()
    .WithStrategy("whitelist-first")
    .WithValidationLayers("syntax", "blacklist", "whitelist", "semantic", "context", "rate-limit")
    .WithInputConstraints(c =>
    {
        c.MaxCommandLength = 5000;
        c.MaxParameters = 25;
        c.RequireFullCmdletNames = true;
    })
    .WithBlacklist(b =>
    {
        b.Patterns = new List<string>
        {
            @"Invoke-Expression.*http",
            @"-ComputerName\s+(?!localhost)",
            @"Set-ExecutionPolicy"
        };
    })
    .AddRoleWhitelist(AgentRole.ReadOnly, w =>
    {
        w.SystemCommands = new List<string> { "Get-*", "Test-*" };
        w.AzureCommands = new List<string> { "Get-AzResource", "Get-AzVM" };
    })
    .Build();

builder.Services.Configure<CommandFilteringConfig>(_ => config);
```

## 🔍 Filter Reasons Reference

| Reason | Description | Action |
|--------|-------------|--------|
| `Allowed` | Command passed all checks | Execute |
| `Blacklisted` | Matched dangerous pattern | Block + alert |
| `NotWhitelisted` | Not in role whitelist | Block |
| `InvalidSyntax` | Syntax error | Block |
| `InvalidParameters` | Bad parameter values | Block |
| `SemanticViolation` | Malicious intent detected | Block + alert |
| `ContextViolation` | Role/env constraint violated | Block |
| `RateLimitExceeded` | Too many requests | Block + retry-after |
| `PrivilegeEscalation` | Privilege escalation attempt | Block + alert |
| `LateralMovement` | Remote execution detected | Block + alert |
| `DataExfiltration` | Data exfil pattern detected | Block + alert |
| `ObfuscationDetected` | Obfuscated command | Block + alert |
| `CircuitBreakerOpen` | Circuit breaker tripped | Block |

## 🚨 Common Blacklist Patterns

```regex
# Command injection
;\\s*rm\\s+-rf
;\\s*shutdown
\\|\\s*bash
\\$\\([^)]*\\)        # Command substitution

# Data exfiltration
\\|\\s*(curl|wget).*http
Invoke-WebRequest.*-Method\\s+POST

# Privilege escalation
Set-ExecutionPolicy
Add-LocalGroupMember.*Administrators
sudo|runas

# Lateral movement
-ComputerName\\s+(?!localhost|127\\.0\\.0\\.1|\\.)
Invoke-Command.*-ComputerName

# Obfuscation
base64\\s+--decode
FromBase64String

# Path traversal
\\.\\.\\/\\.\\.\\/+
```

## 📈 Performance Tips

1. **Pre-compile Regex**: All patterns compiled at startup
2. **Cache Whitelists**: Loaded once from config
3. **Session State**: In-memory for fast lookups
4. **Async Throughout**: All operations async-capable
5. **Structured Logging**: Use for aggregation/analysis

## 🧪 Testing Examples

```csharp
[Fact]
public async Task ReadOnly_CanExecute_GetCommands()
{
    var context = AgentContext.CreateReadOnly("agent-1", "session-1", AgentEnvironment.Development);
    var result = await _engine.FilterCommandAsync(context, "Get-Process");

    Assert.True(result.IsAllowed);
    Assert.Equal(FilterReason.Allowed, result.Reason);
}

[Fact]
public async Task ReadOnly_Cannot_SetCommands()
{
    var context = AgentContext.CreateReadOnly("agent-1", "session-1", AgentEnvironment.Development);
    var result = await _engine.FilterCommandAsync(context, "Set-Service -Name W32Time");

    Assert.False(result.IsAllowed);
    Assert.Equal(FilterReason.ContextViolation, result.Reason);
}

[Fact]
public async Task Blacklist_Blocks_InjectionPatterns()
{
    var context = new AgentContext { /* ... */ };
    var result = await _engine.FilterCommandAsync(context, "Get-Process; rm -rf /");

    Assert.False(result.IsAllowed);
    Assert.Equal(FilterReason.Blacklisted, result.Reason);
}
```

## 📚 Further Reading

- **Full Documentation**: [README.md](./README.md)
- **Implementation Guide**: [IMPLEMENTATION_SUMMARY.md](./IMPLEMENTATION_SUMMARY.md)
- **Usage Examples**: [CommandFilteringEngineExample.cs](./CommandFilteringEngineExample.cs)
- **Configuration Guide**: See YAML at `/ai-agent-guardrails-enhanced.yml`

## 🆘 Troubleshooting

### Command Always Blocked
1. Check agent role has permission
2. Verify whitelist includes command
3. Check blacklist patterns
4. Review logs for specific reason

### Rate Limit Issues
1. Increase token bucket capacity
2. Adjust refill rate
3. Check for rapid-fire requests
4. Review session state

### Configuration Not Loading
1. Verify YAML path is correct
2. Check YAML syntax
3. Review loader logs
4. Validate config on startup

### False Positives
1. Review semantic analysis patterns
2. Adjust blacklist patterns
3. Add exceptions to whitelist
4. Fine-tune role permissions

---

**Version**: 1.0
**Last Updated**: 2025-10-28
**Target Framework**: .NET 8.0
