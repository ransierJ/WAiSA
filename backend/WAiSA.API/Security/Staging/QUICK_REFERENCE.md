# SecureStagingManager - Quick Reference

## Installation

```bash
# No additional NuGet packages required for .NET 8.0
# Only Microsoft.Extensions.Logging.Abstractions (already in ASP.NET Core)
```

## Setup (Program.cs)

```csharp
using WAiSA.API.Security.Staging;

var builder = WebApplication.CreateBuilder(args);

// Add staging manager
builder.Services.AddSingleton<IStagingManager, SecureStagingManager>();

var app = builder.Build();
app.Run();
```

## Basic Usage

```csharp
// Inject
public class MyService
{
    private readonly IStagingManager _staging;

    public MyService(IStagingManager staging) => _staging = staging;
}

// Use
var env = await _staging.CreateStagingEnvironmentAsync("agent-1", "session-1");
var result = await _staging.ValidateAndStageScriptAsync(env, content, "script.ps1");
if (result.IsValid) {
    // Execute: result.StagedFilePath
}
env.Dispose(); // Auto-cleanup
```

## API Reference

### IStagingManager Methods

```csharp
// Create isolated environment
Task<StagingEnvironment> CreateStagingEnvironmentAsync(
    string agentId,
    string sessionId,
    CancellationToken cancellationToken = default);

// Validate and stage script
Task<ValidationResult> ValidateAndStageScriptAsync(
    StagingEnvironment environment,
    string content,
    string name,
    CancellationToken cancellationToken = default);

// Manual cleanup
Task CleanupStagingEnvironmentAsync(
    StagingEnvironment environment,
    CancellationToken cancellationToken = default);

// Get base path
string GetBaseStagingPath();
```

### StagingEnvironment Properties

```csharp
env.AgentId          // Agent identifier
env.SessionId        // Session identifier
env.RootPath         // Root directory path
env.ScriptsPath      // Scripts directory (0700 → 0400)
env.InputsPath       // Inputs directory (0500)
env.OutputsPath      // Outputs directory (0300)
env.LogsPath         // Logs directory (0200)
env.CreatedAt        // Creation timestamp
env.ExpiresAt        // Expiration timestamp (1 hour)
env.IsDisposed       // Disposal status
env.IsExpired()      // Check if expired
env.Dispose()        // Trigger cleanup
```

### ValidationResult Properties

```csharp
result.IsValid          // Validation status
result.Checksum         // SHA256 hash (64 chars)
result.StagedFilePath   // Full path to staged file
result.Errors           // List of errors
result.Warnings         // List of warnings
result.ValidatedAt      // Validation timestamp
result.FileSizeBytes    // File size
```

## Directory Structure

```
/var/agent-staging/{agentId}_{sessionId}_{timestamp}/
├── scripts/   # 0700 → 0400 (read-only after write)
├── inputs/    # 0500 (read/execute only)
├── outputs/   # 0300 (write/execute only)
└── logs/      # 0200 (write only)
```

## Security Features

### Blocked Filenames
- Contains `..` (path traversal)
- Contains `/` or `\` (separators)
- Contains control characters
- Length > 255 characters
- Non-alphanumeric except `_`, `-`, `.`

### Blocked/Warned Patterns
```
Invoke-Expression, IEX
Invoke-Command, Invoke-WebRequest
Start-Process
New-Object System.Net.WebClient
DownloadString, DownloadFile
exec(), eval()
Base64 encoding
-EncodedCommand
Add-Type
rm -rf, del /
```

## Error Handling

```csharp
try {
    var env = await _staging.CreateStagingEnvironmentAsync(agentId, sessionId);
    var result = await _staging.ValidateAndStageScriptAsync(env, content, name);

    if (!result.IsValid) {
        // Handle validation errors
        foreach (var error in result.Errors) {
            Console.WriteLine(error);
        }
        return;
    }

    // Execute script...

} catch (ArgumentException ex) {
    // Invalid parameters
} catch (UnauthorizedAccessException ex) {
    // Permission denied
} catch (IOException ex) {
    // File system error
} finally {
    env?.Dispose();
}
```

## Common Patterns

### Controller Example

```csharp
[HttpPost("execute")]
public async Task<IActionResult> Execute(
    [FromBody] ScriptRequest request,
    CancellationToken ct)
{
    StagingEnvironment env = null;
    try {
        env = await _staging.CreateStagingEnvironmentAsync(
            request.AgentId, request.SessionId, ct);

        var validation = await _staging.ValidateAndStageScriptAsync(
            env, request.Content, request.Name, ct);

        if (!validation.IsValid)
            return BadRequest(validation.Errors);

        var output = await ExecuteScript(validation.StagedFilePath);

        return Ok(new {
            checksum = validation.Checksum,
            output
        });
    } finally {
        env?.Dispose();
    }
}
```

### Using Statement

```csharp
using (var env = await _staging.CreateStagingEnvironmentAsync(agentId, sessionId))
{
    var result = await _staging.ValidateAndStageScriptAsync(env, content, "script.ps1");
    // ... execute script ...
} // Auto-cleanup on dispose
```

### Manual Cleanup

```csharp
var env = await _staging.CreateStagingEnvironmentAsync(agentId, sessionId);
try {
    // ... use environment ...
} finally {
    await _staging.CleanupStagingEnvironmentAsync(env);
}
```

## Platform Differences

| Feature | Linux/macOS | Windows |
|---------|-------------|---------|
| Base Path | `/var/agent-staging` | `C:\AgentStaging` |
| Permissions | chmod 0700/0500/0300/0200 | FileAttributes |
| Read-only | chmod 0400 | FileAttributes.ReadOnly |

## Auto-Cleanup

- **Expiration**: 1 hour after creation
- **Check**: Every 10 minutes
- **Trigger**: Dispose() or timer
- **Secure**: 3-pass DoD 5220.22-M overwrite

## Testing

```csharp
// xUnit test example
[Fact]
public async Task ValidScript_ReturnsSuccess()
{
    var env = await _staging.CreateStagingEnvironmentAsync("test-agent", "test-session");
    var result = await _staging.ValidateAndStageScriptAsync(
        env, "Get-Process", "script.ps1");

    Assert.True(result.IsValid);
    Assert.NotEmpty(result.Checksum);

    await _staging.CleanupStagingEnvironmentAsync(env);
}
```

## Performance Tips

1. Reuse IStagingManager instance (singleton)
2. Dispose environments promptly
3. Use CancellationToken for long operations
4. Don't stage large files unnecessarily
5. Monitor disk space

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "Permission denied" | Ensure app has write access to base path |
| "Path traversal detected" | Use alphanumeric + `_-.` only |
| "Dangerous pattern" | Review script content, remove risky commands |
| Environment not cleaned | Check logs, verify timer is running |
| Checksum mismatch | File modified after staging |

## Configuration

```csharp
// Optionally customize base path
var staging = new SecureStagingManager(logger);
var basePath = staging.GetBaseStagingPath();
// Ensure directory exists before first use
Directory.CreateDirectory(basePath);
```

## Monitoring

Log events to watch:
- `CreateStagingEnvironmentAsync` - track creation rate
- `ValidateAndStageScriptAsync` - validation failures
- `CleanupStagingEnvironmentAsync` - cleanup errors
- Periodic cleanup - timer execution

## Support

- **Documentation**: README.md
- **Tests**: SecureStagingManagerTests.cs
- **Examples**: Examples/AgentExecutionController.cs
- **Full Summary**: IMPLEMENTATION_SUMMARY.md

---

**Version**: 1.0.0
**Date**: 2025-10-28
**Target**: .NET 8.0
**Status**: Production-Ready ✓
