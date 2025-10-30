# Command Filtering Engine

A comprehensive multi-layer command filtering system for AI agent security in ASP.NET Core 8.0.

## Overview

The `CommandFilteringEngine` provides defense-in-depth security for AI agents by implementing multiple validation layers:

1. **Syntax Validation** - Validates command structure and format
2. **Blacklist Filtering** - Blocks known dangerous patterns
3. **Whitelist Filtering** - Enforces role-based command permissions
4. **Semantic Analysis** - Detects malicious intent using pattern matching
5. **Context Validation** - Validates commands against agent context and session state
6. **Rate Limiting** - Prevents resource exhaustion and abuse

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                   CommandFilteringEngine                     │
├─────────────────────────────────────────────────────────────┤
│  Layer 1: Syntax Validation                                 │
│    ├─ Balanced quotes/brackets                              │
│    ├─ Command length limits                                 │
│    └─ Control character detection                           │
├─────────────────────────────────────────────────────────────┤
│  Layer 2: Blacklist Check                                   │
│    ├─ Dangerous pattern matching                            │
│    ├─ Privilege escalation detection                        │
│    └─ Lateral movement detection                            │
├─────────────────────────────────────────────────────────────┤
│  Layer 3: Whitelist Check                                   │
│    ├─ Role-based permissions (Manual → FullAutonomy)        │
│    ├─ Environment-specific rules (Dev vs Prod)              │
│    └─ Command category filtering (System, Azure, FileSystem)│
├─────────────────────────────────────────────────────────────┤
│  Layer 4: Semantic Analysis                                 │
│    ├─ Threat pattern detection                              │
│    ├─ Data exfiltration detection                           │
│    └─ Command combination analysis                          │
├─────────────────────────────────────────────────────────────┤
│  Layer 5: Context Validation                                │
│    ├─ Session anomaly detection                             │
│    ├─ Role constraint enforcement                           │
│    └─ Environment policy validation                         │
├─────────────────────────────────────────────────────────────┤
│  Layer 6: Rate Limiting                                     │
│    ├─ Token bucket algorithm                                │
│    ├─ Per-minute/hour limits                                │
│    └─ Burst protection                                      │
└─────────────────────────────────────────────────────────────┘
```

## Components

### Core Classes

- **`ICommandFilteringEngine`** - Main filtering interface
- **`CommandFilteringEngine`** - Multi-layer filtering implementation
- **`AgentContext`** - Agent execution context (role, environment, session)
- **`CommandFilterResult`** - Filtering decision with detailed reasoning

### Supporting Services

- **`ISemanticAnalyzer`** - Detects malicious patterns and intent
- **`IContextValidator`** - Validates against agent context and session state
- **`IRateLimiter`** - Token bucket rate limiting

### Configuration

- **`CommandFilteringConfig`** - Main configuration class
- **`GuardrailsConfigurationLoader`** - YAML configuration loader
- **`InputConstraints`** - Input validation rules
- **`BlacklistConfig`** - Dangerous pattern configuration
- **`RoleWhitelist`** - Role-based command permissions

### Models

- **`AgentRole`** - Autonomy levels (Manual → FullAutonomy)
- **`AgentEnvironment`** - Deployment environments (Dev, Prod, Staging, Isolated)
- **`FilterReason`** - Detailed blocking reasons
- **`AllowedParameters`** - Parameter validation rules

## Usage

### 1. Service Registration

```csharp
// In Program.cs or Startup.cs
builder.Services.AddCommandFiltering(
    builder.Configuration,
    configPath: "ai-agent-guardrails-enhanced.yml");
```

### 2. Inject and Use

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
        // Create agent context
        var context = new AgentContext
        {
            AgentId = request.AgentId,
            SessionId = request.SessionId,
            Role = AgentRole.ReadOnly,
            Environment = AgentEnvironment.Production,
            UserId = User.Identity?.Name
        };

        // Filter command
        var result = await _filteringEngine.FilterCommandAsync(
            context,
            request.Command,
            request.Parameters);

        if (!result.IsAllowed)
        {
            return BadRequest(new
            {
                Error = "Command blocked",
                Reason = result.Reason.ToString(),
                Message = result.Message,
                DecisionId = result.DecisionId
            });
        }

        if (result.RequiresApproval)
        {
            // Queue for human approval
            return Accepted(new
            {
                Status = "PendingApproval",
                DecisionId = result.DecisionId
            });
        }

        // Execute command
        // ... execution logic ...

        return Ok();
    }
}
```

### 3. Configure Whitelists

In `ai-agent-guardrails-enhanced.yml`:

```yaml
agent_security:
  command_filtering:
    enabled: true
    strategy: "whitelist-first"
    validation_layers:
      - "syntax"
      - "blacklist"
      - "whitelist"
      - "semantic"
      - "context"
      - "rate-limit"

  information_gathering:
    allowed_cmdlets:
      system:
        - "Get-Process"
        - "Get-Service"
        - "Get-EventLog"
      azure:
        - "Get-AzResource"
        - "Get-AzVM"
      filesystem:
        - "Get-ChildItem"
        - "Get-Content"

  autonomy_tiers:
    tier_1_read_only:
      auto_approve_commands:
        - "Get-*"
        - "Test-*"
```

## Agent Roles

### Tier 0: Manual
- All commands require human approval
- AI suggests, human executes
- Suitable for: Initial deployment, high-risk operations

### Tier 1: ReadOnly
- Auto-approve safe read operations (Get-*, Test-*, Measure-*)
- Block all write operations
- Suitable for: Information gathering, diagnostics

### Tier 2: LimitedWrite
- ReadOnly permissions + safe service management
- Auto-approve: Restart-Service, Write-EventLog
- Require approval: Stop-Service, Set-Service
- Suitable for: Service management, log collection

### Tier 3: Supervised
- Broader automation with supervision
- Block destructive operations (Remove-*, Clear-*, Format-*)
- Require approval for high-risk operations
- Suitable for: Routine maintenance, configuration management

### Tier 4: FullAutonomy
- Complete automation (restricted environments only)
- Only allowed in Isolated/non-production environments
- Requires complete audit logging and rollback capability
- Suitable for: Lab environments, testing

## Environment Policies

### Development
- Relaxed restrictions
- All roles allowed
- Extended whitelists

### Production
- Strict security controls
- FullAutonomy role blocked
- Reduced whitelists
- Additional approval requirements

### Staging
- Moderate restrictions
- Testing production policies

### Isolated
- Air-gapped environments
- Full autonomy allowed
- Complete audit logging required

## Security Features

### Blacklist Patterns

The engine blocks dangerous patterns including:

- **Command Injection**: `; rm -rf`, `&& shutdown`, `| bash`
- **Data Exfiltration**: `Invoke-WebRequest -Method POST`, `curl --data`
- **Privilege Escalation**: `Set-ExecutionPolicy`, `Add-LocalGroupMember`
- **Obfuscation**: Base64 encoding, URL encoding, hex encoding
- **Path Traversal**: `../../etc/passwd`
- **Lateral Movement**: Remote execution on non-localhost targets

### Parameter Validation

- Maximum parameter count (default: 50)
- Maximum parameter length (default: 1000 chars)
- Pattern matching for allowed values
- Injection detection (SQL, XSS, command injection)
- Path traversal prevention

### Rate Limiting

- **Token Bucket Algorithm**
  - Capacity: 100 tokens
  - Refill rate: 10 tokens/second
  - Burst allowance: 20 extra tokens

- **Per-Minute Limits**: 100 requests
- **Per-Hour Limits**: 1000 requests
- **Concurrent Operations**: 10 max

### Semantic Analysis

Detects malicious intent through pattern matching:

- Privilege escalation indicators
- Lateral movement patterns
- Data exfiltration attempts
- Credential theft patterns
- Obfuscation techniques
- Persistence mechanisms
- Destructive operations
- Remote code execution

## Configuration

### YAML Configuration

```yaml
agent_security:
  command_filtering:
    enabled: true
    strategy: "whitelist-first"

    input_constraints:
      max_command_length: 10000
      max_parameters: 50
      max_parameter_length: 1000
      require_full_cmdlet_names: true

    blacklist:
      enabled: true
      patterns:
        - "Invoke-Expression.*http"
        - "IEX.*DownloadString"
        - "-ComputerName\\s+(?!localhost)"
```

### Programmatic Configuration

```csharp
var config = new CommandFilteringConfigBuilder()
    .EnableFiltering()
    .WithStrategy("whitelist-first")
    .WithValidationLayers("syntax", "blacklist", "whitelist", "semantic", "context")
    .WithInputConstraints(constraints =>
    {
        constraints.MaxCommandLength = 10000;
        constraints.RequireFullCmdletNames = true;
    })
    .WithBlacklist(blacklist =>
    {
        blacklist.Patterns = new List<string>
        {
            @"Invoke-Expression.*http",
            @"Set-ExecutionPolicy"
        };
    })
    .AddRoleWhitelist(AgentRole.ReadOnly, whitelist =>
    {
        whitelist.SystemCommands = new List<string> { "Get-*", "Test-*" };
    })
    .Build();
```

## Logging

The engine provides comprehensive logging at different levels:

```csharp
// Information: Configuration loaded, filtering completed
_logger.LogInformation("Command filtering completed. IsAllowed={IsAllowed}", result.IsAllowed);

// Warning: Blacklist violations, rate limits, semantic threats
_logger.LogWarning("Command blocked by blacklist pattern. Pattern={Pattern}", pattern);

// Debug: Validation details
_logger.LogDebug("Semantic analysis passed for command: {Command}", command);

// Error: Internal errors
_logger.LogError(ex, "Error filtering command for AgentId={AgentId}", context.AgentId);
```

## Performance

- **Regex Pattern Compilation**: All patterns are pre-compiled for optimal performance
- **Token Bucket Background Task**: Async refill without blocking requests
- **Session State Caching**: In-memory session state tracking
- **Concurrent Dictionary**: Thread-safe rate limit tracking

## Testing

See `CommandFilteringEngineExample.cs` for comprehensive usage examples including:

1. ReadOnly role executing safe commands
2. ReadOnly role blocked from write operations
3. Blacklisted command detection
4. Parameter validation and injection detection
5. Production environment restrictions
6. Semantic analysis of lateral movement

## Dependencies

```xml
<PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Options" Version="8.0.0" />
<PackageReference Include="YamlDotNet" Version="13.7.1" />
```

## License

MIT License - See LICENSE file for details

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## Support

For issues or questions:
- GitHub Issues: [Repository Issues](https://github.com/your-org/waisa)
- Documentation: [Full Documentation](https://docs.waisa.ai)
