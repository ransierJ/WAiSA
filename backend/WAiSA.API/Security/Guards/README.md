# Lateral Movement Guard

## Overview

The `LateralMovementGuard` class provides comprehensive protection against lateral movement attempts by AI agents in the WAiSA (Windows/Azure Infrastructure Sysadmin Agent) system. It validates commands to prevent execution on remote systems and restricts access to internal networks.

## Features

- **Remote Cmdlet Blocking**: Prevents execution of PowerShell remote execution cmdlets (Enter-PSSession, Invoke-Command, etc.)
- **Remote Parameter Detection**: Blocks commands with -ComputerName parameters targeting non-localhost systems
- **Protocol Restriction**: Prevents WinRM and SSH protocol usage
- **Network Range Filtering**: Blocks access to internal network IP ranges (10.x, 172.x, 192.168.x)
- **Thread-Safe**: Concurrent command validation with proper locking mechanisms
- **Configuration Reload**: Hot-reload of configuration without service restart
- **Comprehensive Logging**: Security event logging for audit trails

## Installation

### 1. Add Required NuGet Packages

Add the following package to your `WAiSA.API.csproj`:

```xml
<PackageReference Include="YamlDotNet" Version="13.7.1" />
```

### 2. Register in Dependency Injection

In `Program.cs`:

```csharp
using WAiSA.API.Security.Guards;

// Register LateralMovementGuard
builder.Services.AddSingleton<ILateralMovementGuard>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<LateralMovementGuard>>();
    var configPath = Path.Combine(
        builder.Environment.ContentRootPath,
        "..",
        "..",
        "ai-agent-guardrails-enhanced.yml");
    return new LateralMovementGuard(logger, configPath);
});
```

## Usage

### Basic Usage

```csharp
public class CommandExecutionService
{
    private readonly ILateralMovementGuard _guard;
    private readonly ILogger<CommandExecutionService> _logger;

    public CommandExecutionService(
        ILateralMovementGuard guard,
        ILogger<CommandExecutionService> logger)
    {
        _guard = guard;
        _logger = logger;
    }

    public async Task<bool> ExecuteCommandAsync(string command)
    {
        // Validate command before execution
        var validationResult = await _guard.ValidateCommandAsync(command);

        if (!validationResult.IsAllowed)
        {
            _logger.LogWarning(
                "Command blocked: {Reason}. Violation: {Type}",
                validationResult.BlockedReason,
                validationResult.ViolationType);

            if (validationResult.ShouldQuarantine)
            {
                // Quarantine agent logic here
                await QuarantineAgentAsync();
            }

            return false;
        }

        // Execute command
        return await ExecuteAsync(command);
    }
}
```

### With Cancellation Token

```csharp
public async Task ValidateWithTimeoutAsync(string command)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    try
    {
        var result = await _guard.ValidateCommandAsync(command, cts.Token);
        // Process result
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("Validation timeout for command");
    }
}
```

### Configuration Reload

```csharp
public async Task ReloadSecurityConfigurationAsync()
{
    await _guard.ReloadConfigurationAsync();
    _logger.LogInformation("Security configuration reloaded");
}
```

## Validation Results

### ValidationResult Properties

- **IsAllowed** (bool): Whether the command is permitted
- **BlockedReason** (string): Human-readable explanation for blocking
- **ViolationType** (ViolationType?): Type of violation detected
- **ShouldQuarantine** (bool): Whether the agent should be quarantined
- **Context** (Dictionary<string, string>): Additional violation context

### ViolationType Enum

- **RemoteCmdlet**: Blocked remote execution cmdlet detected
- **RemoteParameter**: Remote parameter targeting non-localhost
- **NetworkRestriction**: Internal network access attempt
- **RemoteProtocol**: WinRM or SSH protocol usage

## Configuration

The guard reads configuration from `ai-agent-guardrails-enhanced.yml`:

```yaml
agent_security:
  lateral_movement:
    enabled: true
    block_remote_execution: true

    blocked_cmdlets:
      - "Enter-PSSession"
      - "Invoke-Command"
      - "New-PSSession"
      # ... more cmdlets

    allowed_targets:
      - "localhost"
      - "127.0.0.1"
      - "."
      - "$env:COMPUTERNAME"

    network_restrictions:
      deny_outbound_to_internal_networks: true
      blocked_ports:
        - 22    # SSH
        - 3389  # RDP
        - 5985  # WinRM HTTP
        - 5986  # WinRM HTTPS

    on_violation:
      action: "block"
      notify_security_team: true
      quarantine_agent: true
```

## Blocked Patterns

### Remote Cmdlets

- Enter-PSSession
- Invoke-Command
- New-PSSession
- Connect-PSSession
- Remove-PSSession
- Get-PSSession
- New-CimSession
- Invoke-WmiMethod
- Invoke-CimMethod

### Remote Protocols

- winrs (Windows Remote Shell)
- ssh (Secure Shell)

### Network Ranges

- 10.0.0.0/8
- 172.16.0.0/12
- 192.168.0.0/16

## Examples

### Blocked Commands

```powershell
# Blocked - Remote cmdlet
Enter-PSSession -ComputerName RemoteServer

# Blocked - Remote parameter
Get-Service -ComputerName 192.168.1.100

# Blocked - WinRM
winrs -r:RemoteServer ipconfig

# Blocked - SSH
ssh user@remotehost 'ls -la'

# Blocked - Internal network
Test-Connection 10.0.0.5
```

### Allowed Commands

```powershell
# Allowed - Localhost target
Get-Service -ComputerName localhost

# Allowed - Local command
Get-Process | Where-Object { $_.CPU -gt 100 }

# Allowed - 127.0.0.1
Restart-Service -ComputerName 127.0.0.1 -Name "MyService"
```

## Thread Safety

The guard is thread-safe and uses `SemaphoreSlim` for configuration access synchronization. Multiple concurrent validations can be performed safely.

```csharp
// Safe concurrent validation
var tasks = commands.Select(cmd => guard.ValidateCommandAsync(cmd));
var results = await Task.WhenAll(tasks);
```

## Performance

- Compiled regular expressions for fast pattern matching
- Regex timeout protection (100ms) to prevent ReDoS attacks
- Minimal memory allocation during validation
- Configuration caching with reload capability

## Security Event Logging

All security events are logged with appropriate severity levels:

- **Warning**: Blocked commands, violations detected
- **Information**: Configuration reload, initialization
- **Error**: Configuration load failures, validation errors
- **Debug**: Allowed commands (when enabled)

## Error Handling

The guard implements fail-secure behavior:

- Configuration load failures throw exceptions at startup
- Validation errors return blocked result (deny by default)
- Cancellation properly propagated through async chain
- All exceptions logged with context

## Testing

Comprehensive unit tests are provided in `LateralMovementGuardTests.cs`:

```bash
dotnet test --filter "FullyQualifiedName~LateralMovementGuardTests"
```

Test coverage includes:
- All violation types
- Concurrent validations
- Configuration reload
- Edge cases (null, empty commands)
- Cancellation handling

## Integration with WAiSA

### In Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class CommandController : ControllerBase
{
    private readonly ILateralMovementGuard _guard;

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteCommand([FromBody] CommandRequest request)
    {
        var validation = await _guard.ValidateCommandAsync(request.Command);

        if (!validation.IsAllowed)
        {
            return BadRequest(new
            {
                error = validation.BlockedReason,
                violationType = validation.ViolationType?.ToString(),
                quarantine = validation.ShouldQuarantine
            });
        }

        // Execute command
        return Ok();
    }
}
```

### In Middleware

```csharp
public class CommandValidationMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(
        HttpContext context,
        ILateralMovementGuard guard)
    {
        // Extract command from request
        var command = await ExtractCommandAsync(context.Request);

        if (command != null)
        {
            var validation = await guard.ValidateCommandAsync(command);

            if (!validation.IsAllowed)
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = validation.BlockedReason
                });
                return;
            }
        }

        await _next(context);
    }
}
```

## Monitoring and Alerts

The guard integrates with Application Insights for security monitoring:

```csharp
// In your telemetry initialization
services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});
```

Security events are automatically tracked and can trigger alerts based on:
- Violation frequency
- Violation types
- Quarantine events

## Best Practices

1. **Always validate before execution**: Never execute commands without validation
2. **Log all violations**: Security events are critical for audit trails
3. **Implement quarantine**: Respect the `ShouldQuarantine` flag
4. **Monitor patterns**: Track violation patterns for threat detection
5. **Regular config review**: Periodically review and update blocked cmdlets
6. **Test configuration changes**: Validate config changes in non-production first

## Troubleshooting

### Configuration Not Loading

Check the file path and ensure `ai-agent-guardrails-enhanced.yml` exists:

```csharp
var configPath = Path.GetFullPath("../../ai-agent-guardrails-enhanced.yml");
Console.WriteLine($"Config path: {configPath}");
Console.WriteLine($"Exists: {File.Exists(configPath)}");
```

### Commands Incorrectly Blocked

Enable debug logging to see validation details:

```json
{
  "Logging": {
    "LogLevel": {
      "WAiSA.API.Security.Guards.LateralMovementGuard": "Debug"
    }
  }
}
```

### Performance Issues

Check regex timeout warnings in logs. If patterns are too complex, consider:
- Simplifying patterns
- Increasing timeout values
- Pre-filtering obvious safe commands

## Future Enhancements

- Machine learning-based anomaly detection
- Custom rule definitions via API
- Real-time threat intelligence integration
- Behavioral pattern analysis
- Integration with Azure Sentinel

## License

Copyright (c) 2025 WAiSA Project
