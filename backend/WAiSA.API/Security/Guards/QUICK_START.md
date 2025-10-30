# LateralMovementGuard - Quick Start Guide

## 30-Second Setup

### 1. Install (Already Done ✅)
The YamlDotNet package is already added to the project.

### 2. Register in Program.cs

```csharp
using WAiSA.API.Security.Guards;

// Add this line before builder.Build()
builder.Services.AddSingleton<ILateralMovementGuard>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<LateralMovementGuard>>();
    var configPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "..", "..",
        "ai-agent-guardrails-enhanced.yml");
    return new LateralMovementGuard(logger, configPath);
});

var app = builder.Build();
```

### 3. Use in Your Code

```csharp
public class YourController : ControllerBase
{
    private readonly ILateralMovementGuard _guard;

    public YourController(ILateralMovementGuard guard)
    {
        _guard = guard;
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] string command)
    {
        var result = await _guard.ValidateCommandAsync(command);

        if (!result.IsAllowed)
        {
            return Forbid(result.BlockedReason);
        }

        // Execute command
        return Ok();
    }
}
```

## What Gets Blocked?

### ❌ Remote Cmdlets
```powershell
Enter-PSSession -ComputerName Remote  # BLOCKED
Invoke-Command -ComputerName Server   # BLOCKED
New-PSSession                         # BLOCKED
```

### ❌ Remote Parameters
```powershell
Get-Service -ComputerName 10.0.0.5    # BLOCKED
Restart-Computer -ComputerName Remote # BLOCKED
```

### ❌ Remote Protocols
```powershell
winrs -r:RemoteServer ipconfig        # BLOCKED
ssh user@host 'ls -la'                # BLOCKED
```

### ❌ Internal Networks
```powershell
Test-Connection 192.168.1.100         # BLOCKED
Invoke-WebRequest http://10.0.0.1     # BLOCKED
ping 172.16.0.1                       # BLOCKED
```

## What's Allowed?

### ✅ Localhost Commands
```powershell
Get-Service -ComputerName localhost   # ALLOWED
Restart-Service -ComputerName 127.0.0.1  # ALLOWED
Get-Process                           # ALLOWED
```

### ✅ Safe Operations
```powershell
Get-Process | Where-Object { $_.CPU -gt 100 }  # ALLOWED
Get-EventLog -LogName Application              # ALLOWED
Test-Path C:\Temp                              # ALLOWED
```

## ValidationResult Properties

```csharp
var result = await _guard.ValidateCommandAsync(command);

// Check these properties:
result.IsAllowed         // true if safe, false if blocked
result.BlockedReason     // "Why was it blocked?"
result.ViolationType     // RemoteCmdlet | RemoteParameter | NetworkRestriction | RemoteProtocol
result.ShouldQuarantine  // true if agent should be quarantined
result.Context           // Additional details (Dictionary<string, string>)
```

## Full Example with Quarantine

```csharp
[HttpPost("execute")]
public async Task<IActionResult> ExecuteCommand([FromBody] CommandRequest request)
{
    var validation = await _guard.ValidateCommandAsync(request.Command);

    if (!validation.IsAllowed)
    {
        _logger.LogWarning(
            "Command blocked: {Reason}, Type: {Type}",
            validation.BlockedReason,
            validation.ViolationType);

        if (validation.ShouldQuarantine)
        {
            await QuarantineAgentAsync();
        }

        return StatusCode(403, new
        {
            error = validation.BlockedReason,
            violationType = validation.ViolationType?.ToString(),
            quarantined = validation.ShouldQuarantine
        });
    }

    // Safe to execute
    var output = await ExecuteCommandInternal(request.Command);
    return Ok(new { output });
}
```

## Configuration (ai-agent-guardrails-enhanced.yml)

The guard automatically loads settings from:
```
/home/sysadmin/sysadmin_in_a_box/ai-agent-guardrails-enhanced.yml
```

To reload config without restart:
```csharp
await _guard.ReloadConfigurationAsync();
```

## Logging

All security events are automatically logged:

```
[Warning] Blocked remote cmdlet detected: Enter-PSSession in command: Enter-PSSession...
[Information] LateralMovementGuard initialized. Enabled: true, Blocked cmdlets: 10
[Error] Failed to load configuration from /path/to/config.yml
```

Enable debug logging in appsettings.json:
```json
{
  "Logging": {
    "LogLevel": {
      "WAiSA.API.Security.Guards.LateralMovementGuard": "Debug"
    }
  }
}
```

## Testing Your Integration

### Test Blocked Command:
```bash
curl -X POST https://localhost:5001/api/command/execute \
  -H "Content-Type: application/json" \
  -d '{"command": "Enter-PSSession -ComputerName Remote"}'

# Expected: 403 Forbidden
# { "error": "Remote execution cmdlet 'Enter-PSSession' is not permitted" }
```

### Test Allowed Command:
```bash
curl -X POST https://localhost:5001/api/command/execute \
  -H "Content-Type: application/json" \
  -d '{"command": "Get-Process"}'

# Expected: 200 OK
```

## Common Patterns

### Pattern 1: Validate Before Execute
```csharp
var validation = await _guard.ValidateCommandAsync(command);
if (validation.IsAllowed)
{
    await ExecuteAsync(command);
}
```

### Pattern 2: Log and Alert
```csharp
if (!validation.IsAllowed)
{
    _logger.LogWarning("Security violation: {Type}", validation.ViolationType);
    await _alertService.SendSecurityAlertAsync(validation);
}
```

### Pattern 3: Quarantine on Violation
```csharp
if (validation.ShouldQuarantine)
{
    await _agentService.QuarantineAsync(userId);
    await _notificationService.NotifySecurityTeamAsync(validation);
}
```

## Troubleshooting

### "Configuration file not found"
Check the path in Program.cs matches your file location:
```csharp
var configPath = Path.GetFullPath("../../ai-agent-guardrails-enhanced.yml");
Console.WriteLine($"Config path: {configPath}");
Console.WriteLine($"Exists: {File.Exists(configPath)}");
```

### "Commands incorrectly blocked"
Enable debug logging to see validation details.

### "Performance issues"
The guard is highly optimized. If issues persist:
- Check for extremely long commands
- Review regex timeout warnings in logs
- Consider pre-filtering obvious safe commands

## Thread Safety

✅ **Yes!** Safe to use concurrently:
```csharp
var tasks = commands.Select(cmd => _guard.ValidateCommandAsync(cmd));
var results = await Task.WhenAll(tasks);
```

## Advanced Usage

See the complete documentation:
- **README.md** - Full documentation
- **IntegrationExample.cs** - Controller and middleware examples
- **IMPLEMENTATION_SUMMARY.md** - Technical details

## Support

For issues or questions:
1. Check logs for detailed error messages
2. Review the README.md documentation
3. Check IMPLEMENTATION_SUMMARY.md for technical details
4. Contact the WAiSA security team

---

**That's it!** You're now protecting your API from lateral movement attempts. 🛡️
