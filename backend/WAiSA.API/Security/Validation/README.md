# InputValidator - AI Agent Input Validation & Sanitization

## Overview

The `InputValidator` class provides comprehensive validation and sanitization of AI agent inputs to prevent injection attacks, including:

- Command injection
- Path traversal
- Encoding-based bypasses
- Malicious parameter injection
- Null byte injection

## Features

### 1. Command Validation
- **Syntax Validation**: Checks for balanced quotes, brackets, and parentheses
- **Length Constraints**: Enforces maximum command length (10,000 chars)
- **Unicode Normalization**: Normalizes input to prevent unicode-based attacks
- **Null Byte Detection**: Detects and rejects null bytes in input

### 2. Injection Pattern Detection
Detects the following dangerous patterns:

#### Command Injection
- `;rm`, `;del`, `;format`, `;shutdown`, `;reboot`
- Fork bombs: `:(){:|:&};:`
- Dangerous commands after separators

#### Pipe Injection
- `|bash`, `|sh`, `|cmd`, `|powershell`
- `|python`, `|perl`, `|ruby`, `|node`

#### Subshell & Command Substitution
- `$(command)`
- `` `command` ``
- `$((expression))`

#### Logical Operators
- `&& rm`, `|| cat`, `&& curl`, `&& wget`

#### Redirection
- `> /dev/`, `>> /proc/`, `< /sys/`
- Redirects to system paths or drives

#### Environment Variables
- `${VAR}`, `$VAR`

### 3. Encoding Detection
Detects attempts to bypass filters using:
- **Base64**: Long base64-encoded strings
- **Hex**: `0x` prefixed hex values or `\x` escape sequences
- **Unicode**: `\u0000` or `\U00000000` escapes
- **URL Encoding**: `%XX` sequences
- **HTML Entities**: `&entity;` or `&#num;`

### 4. Path Traversal Detection
Detects:
- `../` sequences (directory traversal)
- Absolute Unix paths (`/etc/passwd`)
- Absolute Windows paths (`C:\Windows`)
- Home directory references (`~/`)
- UNC paths (`\\server\share`)

### 5. Parameter Sanitization
- Removes dangerous characters: `; & | ` $ ( ) < > \ \n \r \0`
- Validates parameter names (alphanumeric, `_`, `-` only)
- Escapes special characters (`"`, `'`)
- Enforces parameter count limit (50 max)
- Enforces parameter name/value length limits

## Usage

### Basic Setup

```csharp
// In Program.cs or Startup.cs
builder.Services.AddScoped<IInputValidator, InputValidator>();
```

### Validate Command

```csharp
public class AgentController : ControllerBase
{
    private readonly IInputValidator _validator;

    public AgentController(IInputValidator validator)
    {
        _validator = validator;
    }

    [HttpPost("execute")]
    public IActionResult ExecuteCommand([FromBody] CommandRequest request)
    {
        // Validate command and parameters
        var result = _validator.ValidateCommand(request.Command, request.Parameters);

        if (!result.IsValid)
        {
            return BadRequest(new
            {
                Message = "Invalid command detected",
                Severity = result.Severity.ToString(),
                Failures = result.Failures.Select(f => new
                {
                    f.Type,
                    f.Message,
                    f.Pattern,
                    f.Context
                })
            });
        }

        // Safe to proceed with command execution
        return Ok();
    }
}
```

### Check for Injection Patterns

```csharp
var result = _validator.CheckForInjectionPatterns(userInput);

if (!result.IsValid)
{
    foreach (var failure in result.Failures)
    {
        _logger.LogWarning(
            "Injection pattern detected: {Type} - {Message}. Pattern: {Pattern}",
            failure.Type,
            failure.Message,
            failure.Pattern);
    }
}
```

### Sanitize Parameters

```csharp
var unsafeParams = new Dictionary<string, string>
{
    { "filename", "test;rm -rf /" },
    { "path", "../../etc/passwd" }
};

var safeParams = _validator.SanitizeParameters(unsafeParams);
// Result: dangerous characters removed, parameter names validated
```

### Check Path Traversal

```csharp
var violations = _validator.CheckForPathTraversal(parameters);

if (violations.Any())
{
    foreach (var violation in violations)
    {
        _logger.LogWarning(
            "Path traversal detected in parameter '{Parameter}': {Type} - {Description}",
            violation.ParameterName,
            violation.Type,
            violation.Description);
    }
}
```

## Validation Result Structure

```csharp
public class ValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<ValidationFailure> Failures { get; }
    public ValidationSeverity Severity { get; }
}

public class ValidationFailure
{
    public ValidationFailureType Type { get; }
    public string Message { get; }
    public string? Pattern { get; }
    public ValidationSeverity Severity { get; }
    public string? Context { get; }
}

public enum ValidationSeverity
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
```

## Security Best Practices

1. **Always validate before execution**: Never execute commands without validation
2. **Check severity levels**: Reject `High` and `Critical` severity failures
3. **Log all failures**: Maintain audit trail of validation failures
4. **Sanitize after validation**: Use `SanitizeParameters()` even after validation
5. **Whitelist approach**: Prefer whitelisting allowed commands over blacklisting
6. **Defense in depth**: Combine with OS-level sandboxing and permissions

## Configuration Limits

```csharp
private const int MaxCommandLength = 10000;
private const int MaxParameterCount = 50;
private const int MaxParameterNameLength = 100;
private const int MaxParameterValueLength = 5000;
```

## Testing

Comprehensive unit tests are provided in `InputValidatorTests.cs`:

```bash
dotnet test --filter "FullyQualifiedName~InputValidatorTests"
```

Test coverage includes:
- Valid input scenarios
- Command injection attempts
- Path traversal attacks
- Encoding bypass attempts
- Syntax validation
- Parameter sanitization
- Integration tests with complex attack vectors

## Performance

The validator uses:
- **Compiled regex patterns**: Generated at compile-time for optimal performance
- **Single-pass validation**: Minimizes string iterations
- **Early exit**: Returns on critical failures
- **Memory efficient**: Uses `StringBuilder` for sanitization

## Extending the Validator

To add custom validation patterns:

```csharp
public partial class InputValidator
{
    [GeneratedRegex(@"your-pattern-here", RegexOptions.Compiled)]
    private static partial Regex CustomPatternRegex();
}
```

Add to the appropriate pattern array in the constructor.

## License

Part of the WAiSA.API project.
