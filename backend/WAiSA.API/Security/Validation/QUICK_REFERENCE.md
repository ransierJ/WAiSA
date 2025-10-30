# InputValidator Quick Reference

## Quick Start

### 1. Register Service
```csharp
// In Program.cs
builder.Services.AddInputValidation();
```

### 2. Inject and Use
```csharp
public class MyController : ControllerBase
{
    private readonly IInputValidator _validator;

    public MyController(IInputValidator validator)
    {
        _validator = validator;
    }

    [HttpPost]
    public IActionResult Execute([FromBody] Request req)
    {
        var result = _validator.ValidateCommand(req.Command, req.Params);
        if (!result.IsValid)
            return BadRequest(result.Failures);

        // Safe to execute
        return Ok();
    }
}
```

## Common Patterns

### Pattern 1: Basic Validation
```csharp
var result = _validator.ValidateCommand("list users");
if (result.IsValid) { /* proceed */ }
```

### Pattern 2: Validate with Parameters
```csharp
var params = new Dictionary<string, string>
{
    { "userId", "123" },
    { "filter", "active" }
};

var result = _validator.ValidateCommand("get user", params);
```

### Pattern 3: Check Severity
```csharp
var result = _validator.ValidateCommand(cmd);
if (result.Severity >= ValidationSeverity.High)
{
    // Block execution
    _logger.LogWarning("Critical threat detected");
    return;
}
```

### Pattern 4: Sanitize Parameters
```csharp
// Remove dangerous characters
var clean = _validator.SanitizeParameters(untrustedParams);
```

### Pattern 5: Path Traversal Check
```csharp
var violations = _validator.CheckForPathTraversal(params);
if (violations.Any())
{
    // Reject request
}
```

## Detected Threats

### Command Injection
- `;rm -rf /`
- `| bash`
- `$(whoami)`
- `` `cmd` ``
- `&& evil`
- `|| evil`

### Path Traversal
- `../../etc/passwd`
- `/etc/shadow`
- `C:\Windows\System32`
- `~/.ssh/id_rsa`
- `\\server\share`

### Encoding Attacks
- Base64: `cmQgLXJm...`
- Hex: `0x726d...`
- Unicode: `\u002f\u0065...`

### Special Characters
Removed by sanitizer:
- `; & | ` $ ( ) < > \ \n \r \0`

## Severity Levels

```
None     → No issues
Low      → Minor issues, can proceed with warning
Medium   → Should reject
High     → Must reject + log
Critical → Must reject + log + alert
```

## Response Handling

### Success Response
```csharp
{
    "IsValid": true,
    "Failures": [],
    "Severity": "None"
}
```

### Failure Response
```csharp
{
    "IsValid": false,
    "Severity": "Critical",
    "Failures": [
        {
            "Type": "CommandInjection",
            "Message": "Dangerous pattern: ';rm -rf'",
            "Pattern": ";(rm|del)\\s",
            "Severity": "Critical",
            "Context": "test ;rm -rf / more"
        }
    ]
}
```

## Best Practices

1. **Always validate before execution**
   ```csharp
   var result = _validator.ValidateCommand(cmd);
   if (!result.IsValid) return;
   // Execute only if valid
   ```

2. **Check severity levels**
   ```csharp
   if (result.Severity >= ValidationSeverity.High)
   {
       // Block and log
   }
   ```

3. **Sanitize after validation**
   ```csharp
   var clean = _validator.SanitizeParameters(params);
   // Use clean params
   ```

4. **Log all failures**
   ```csharp
   foreach (var failure in result.Failures)
   {
       _logger.LogWarning("{Type}: {Message}",
           failure.Type, failure.Message);
   }
   ```

5. **Combine with whitelist**
   ```csharp
   var allowedCommands = new[] { "list", "get", "update" };
   if (!allowedCommands.Contains(cmd) || !result.IsValid)
       return BadRequest();
   ```

## Configuration

### Limits (Configurable in InputValidator.cs)
```csharp
MaxCommandLength = 10000          // chars
MaxParameterCount = 50            // params
MaxParameterNameLength = 100      // chars
MaxParameterValueLength = 5000    // chars
```

### Valid Parameter Names
- Alphanumeric: `a-z`, `A-Z`, `0-9`
- Special: `_`, `-`
- Invalid: `; & | $ @ ! # % etc.`

## Testing

### Run Tests
```bash
dotnet test --filter "InputValidatorTests"
```

### Example Test
```csharp
[Fact]
public void RejectsCommandInjection()
{
    var result = _validator.ValidateCommand(";rm -rf /");
    Assert.False(result.IsValid);
    Assert.Equal(ValidationSeverity.Critical, result.Severity);
}
```

## Performance

- Uses **compiled regex** for speed
- **Single-pass** validation
- **Early exit** on critical failures
- Average validation time: **< 1ms**

## Support

- Full documentation: `README.md`
- Usage examples: `Examples/UsageExamples.cs`
- Unit tests: `InputValidatorTests.cs`
