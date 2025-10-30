using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WAiSA.API.Security.Validation.Models;

namespace WAiSA.API.Security.Validation;

/// <summary>
/// Validates and sanitizes AI agent inputs to prevent injection attacks
/// </summary>
public sealed partial class InputValidator : IInputValidator
{
    private readonly ILogger<InputValidator> _logger;

    private const int MaxCommandLength = 10000;
    private const int MaxParameterCount = 50;
    private const int MaxParameterNameLength = 100;
    private const int MaxParameterValueLength = 5000;

    // Regex patterns for dangerous command injection
    private static readonly Regex[] DangerousPatterns =
    {
        CommandInjectionRegex(),
        PipeInjectionRegex(),
        SubshellInjectionRegex(),
        BacktickInjectionRegex(),
        LogicalOperatorInjectionRegex(),
        RedirectionInjectionRegex(),
        EnvironmentVariableInjectionRegex(),
        ProcessSubstitutionRegex(),
        HereDocumentRegex(),
        CommandSubstitutionRegex()
    };

    // Regex patterns for encoding attempts
    private static readonly Regex[] EncodingPatterns =
    {
        Base64EncodingRegex(),
        HexEncodingRegex(),
        UnicodeEscapeRegex(),
        UrlEncodingRegex(),
        HtmlEntityRegex()
    };

    // Path traversal patterns
    private static readonly Regex[] PathTraversalPatterns =
    {
        DotDotSlashRegex(),
        DotDotBackslashRegex(),
        AbsoluteUnixPathRegex(),
        AbsoluteWindowsPathRegex(),
        HomeDirRegex(),
        UncPathRegex()
    };

    // Characters to remove during sanitization
    private static readonly char[] DangerousCharacters =
        { ';', '&', '|', '`', '$', '(', ')', '<', '>', '\\', '\n', '\r', '\0' };

    public InputValidator(ILogger<InputValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public ValidationResult ValidateCommand(string command, Dictionary<string, string>? parameters = null)
    {
        var failures = new List<ValidationFailure>();

        // Null or empty check
        if (string.IsNullOrEmpty(command))
        {
            failures.Add(new ValidationFailure(
                ValidationFailureType.SyntaxError,
                "Command cannot be null or empty",
                ValidationSeverity.Critical));
            return new ValidationResult(failures);
        }

        // Unicode normalization
        try
        {
            command = command.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Failed to normalize command input");
            failures.Add(new ValidationFailure(
                ValidationFailureType.SyntaxError,
                "Command contains invalid unicode sequences",
                ValidationSeverity.High));
        }

        // Length validation
        if (command.Length > MaxCommandLength)
        {
            failures.Add(new ValidationFailure(
                ValidationFailureType.LengthExceeded,
                $"Command length {command.Length} exceeds maximum {MaxCommandLength}",
                ValidationSeverity.Medium,
                context: $"Length: {command.Length}"));

            _logger.LogWarning("Command length validation failed: {Length} > {MaxLength}",
                command.Length, MaxCommandLength);
        }

        // Null byte detection
        if (command.Contains('\0'))
        {
            failures.Add(new ValidationFailure(
                ValidationFailureType.NullByteDetected,
                "Command contains null bytes",
                ValidationSeverity.Critical,
                pattern: "\\0"));

            _logger.LogWarning("Null byte detected in command");
        }

        // Syntax validation - balanced quotes and brackets
        ValidateSyntax(command, failures);

        // Check for injection patterns
        var injectionResult = CheckForInjectionPatterns(command);
        if (!injectionResult.IsValid)
        {
            failures.AddRange(injectionResult.Failures);
        }

        // Parameter validation
        if (parameters != null)
        {
            ValidateParameters(parameters, failures);
        }

        var result = new ValidationResult(failures);

        if (!result.IsValid)
        {
            _logger.LogWarning("Command validation failed with {Count} failures. Severity: {Severity}",
                failures.Count, result.Severity);
        }

        return result;
    }

    /// <inheritdoc/>
    public ValidationResult CheckForInjectionPatterns(string command)
    {
        var failures = new List<ValidationFailure>();

        if (string.IsNullOrEmpty(command))
        {
            return ValidationResult.Success();
        }

        // Check for dangerous command injection patterns
        foreach (var pattern in DangerousPatterns)
        {
            var matches = pattern.Matches(command);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    failures.Add(new ValidationFailure(
                        ValidationFailureType.CommandInjection,
                        $"Dangerous command injection pattern detected: '{match.Value}'",
                        ValidationSeverity.Critical,
                        pattern: pattern.ToString(),
                        context: GetContext(command, match.Index)));

                    _logger.LogWarning("Command injection pattern detected: {Pattern} at position {Position}",
                        match.Value, match.Index);
                }
            }
        }

        // Check for encoding attempts to bypass filters
        foreach (var pattern in EncodingPatterns)
        {
            var matches = pattern.Matches(command);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    // Check if the encoded content might contain dangerous patterns
                    if (IsSuspiciousEncoding(match.Value))
                    {
                        failures.Add(new ValidationFailure(
                            ValidationFailureType.EncodingAttempt,
                            $"Suspicious encoding attempt detected: '{match.Value}'",
                            ValidationSeverity.High,
                            pattern: pattern.ToString(),
                            context: GetContext(command, match.Index)));

                        _logger.LogWarning("Encoding attempt detected: {Encoding} at position {Position}",
                            match.Value, match.Index);
                    }
                }
            }
        }

        // Check for path traversal in command itself
        foreach (var pattern in PathTraversalPatterns)
        {
            var matches = pattern.Matches(command);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    failures.Add(new ValidationFailure(
                        ValidationFailureType.PathTraversal,
                        $"Path traversal pattern detected: '{match.Value}'",
                        ValidationSeverity.High,
                        pattern: pattern.ToString(),
                        context: GetContext(command, match.Index)));

                    _logger.LogWarning("Path traversal detected: {Pattern} at position {Position}",
                        match.Value, match.Index);
                }
            }
        }

        return new ValidationResult(failures);
    }

    /// <inheritdoc/>
    public Dictionary<string, string> SanitizeParameters(Dictionary<string, string> parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var sanitized = new Dictionary<string, string>(parameters.Count);

        foreach (var kvp in parameters)
        {
            // Validate and sanitize parameter name
            var sanitizedName = SanitizeParameterName(kvp.Key);
            if (string.IsNullOrEmpty(sanitizedName))
            {
                _logger.LogWarning("Parameter name '{Name}' is invalid and was skipped", kvp.Key);
                continue;
            }

            // Sanitize parameter value
            var sanitizedValue = SanitizeParameterValue(kvp.Value);

            sanitized[sanitizedName] = sanitizedValue;
        }

        _logger.LogInformation("Sanitized {Count} parameters", sanitized.Count);
        return sanitized;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PathTraversalViolation> CheckForPathTraversal(Dictionary<string, string> parameters)
    {
        var violations = new List<PathTraversalViolation>();

        if (parameters == null || parameters.Count == 0)
        {
            return violations;
        }

        foreach (var kvp in parameters)
        {
            var value = kvp.Value ?? string.Empty;

            // Check for .. sequences
            if (value.Contains(".."))
            {
                violations.Add(new PathTraversalViolation(
                    kvp.Key,
                    value,
                    PathTraversalType.DotDotSequence,
                    "Parameter contains '..' directory traversal sequence"));
            }

            // Check for absolute Unix paths
            if (value.StartsWith('/') && value.Length > 1)
            {
                violations.Add(new PathTraversalViolation(
                    kvp.Key,
                    value,
                    PathTraversalType.AbsolutePath,
                    "Parameter contains absolute Unix path"));
            }

            // Check for absolute Windows paths
            if (Regex.IsMatch(value, @"^[A-Za-z]:\\"))
            {
                violations.Add(new PathTraversalViolation(
                    kvp.Key,
                    value,
                    PathTraversalType.WindowsPath,
                    "Parameter contains absolute Windows path"));
            }

            // Check for home directory reference
            if (value.StartsWith("~/") || value.StartsWith("~\\"))
            {
                violations.Add(new PathTraversalViolation(
                    kvp.Key,
                    value,
                    PathTraversalType.HomeDirectory,
                    "Parameter contains home directory reference"));
            }

            // Check for UNC paths
            if (value.StartsWith("\\\\") || value.StartsWith("//"))
            {
                violations.Add(new PathTraversalViolation(
                    kvp.Key,
                    value,
                    PathTraversalType.UncPath,
                    "Parameter contains UNC path"));
            }
        }

        if (violations.Count > 0)
        {
            _logger.LogWarning("Found {Count} path traversal violations", violations.Count);
        }

        return violations.AsReadOnly();
    }

    #region Private Helper Methods

    private static void ValidateSyntax(string command, List<ValidationFailure> failures)
    {
        // Check balanced quotes
        var singleQuotes = command.Count(c => c == '\'');
        var doubleQuotes = command.Count(c => c == '"');

        if (singleQuotes % 2 != 0)
        {
            failures.Add(new ValidationFailure(
                ValidationFailureType.SyntaxError,
                "Unbalanced single quotes in command",
                ValidationSeverity.Medium,
                pattern: "'"));
        }

        if (doubleQuotes % 2 != 0)
        {
            failures.Add(new ValidationFailure(
                ValidationFailureType.SyntaxError,
                "Unbalanced double quotes in command",
                ValidationSeverity.Medium,
                pattern: "\""));
        }

        // Check balanced brackets
        var openParen = command.Count(c => c == '(');
        var closeParen = command.Count(c => c == ')');
        var openBrace = command.Count(c => c == '{');
        var closeBrace = command.Count(c => c == '}');
        var openBracket = command.Count(c => c == '[');
        var closeBracket = command.Count(c => c == ']');

        if (openParen != closeParen)
        {
            failures.Add(new ValidationFailure(
                ValidationFailureType.SyntaxError,
                $"Unbalanced parentheses: {openParen} open, {closeParen} close",
                ValidationSeverity.Medium,
                pattern: "()"));
        }

        if (openBrace != closeBrace)
        {
            failures.Add(new ValidationFailure(
                ValidationFailureType.SyntaxError,
                $"Unbalanced braces: {openBrace} open, {closeBrace} close",
                ValidationSeverity.Medium,
                pattern: "{}"));
        }

        if (openBracket != closeBracket)
        {
            failures.Add(new ValidationFailure(
                ValidationFailureType.SyntaxError,
                $"Unbalanced brackets: {openBracket} open, {closeBracket} close",
                ValidationSeverity.Medium,
                pattern: "[]"));
        }
    }

    private void ValidateParameters(Dictionary<string, string> parameters, List<ValidationFailure> failures)
    {
        if (parameters.Count > MaxParameterCount)
        {
            failures.Add(new ValidationFailure(
                ValidationFailureType.LengthExceeded,
                $"Parameter count {parameters.Count} exceeds maximum {MaxParameterCount}",
                ValidationSeverity.Medium));

            _logger.LogWarning("Parameter count validation failed: {Count} > {MaxCount}",
                parameters.Count, MaxParameterCount);
        }

        foreach (var kvp in parameters)
        {
            // Validate parameter name
            if (!IsValidParameterName(kvp.Key))
            {
                failures.Add(new ValidationFailure(
                    ValidationFailureType.InvalidParameterName,
                    $"Invalid parameter name: '{kvp.Key}'",
                    ValidationSeverity.Medium,
                    context: kvp.Key));
            }

            if (kvp.Key.Length > MaxParameterNameLength)
            {
                failures.Add(new ValidationFailure(
                    ValidationFailureType.LengthExceeded,
                    $"Parameter name length exceeds maximum {MaxParameterNameLength}",
                    ValidationSeverity.Low,
                    context: kvp.Key));
            }

            // Validate parameter value
            if (kvp.Value != null && kvp.Value.Length > MaxParameterValueLength)
            {
                failures.Add(new ValidationFailure(
                    ValidationFailureType.LengthExceeded,
                    $"Parameter value length exceeds maximum {MaxParameterValueLength}",
                    ValidationSeverity.Medium,
                    context: kvp.Key));
            }
        }
    }

    private static bool IsValidParameterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // Parameter names must contain only alphanumeric, underscore, or hyphen
        return ParameterNameRegex().IsMatch(name);
    }

    private static string SanitizeParameterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        // Remove any characters that aren't alphanumeric, underscore, or hyphen
        var sanitized = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
            {
                sanitized.Append(c);
            }
        }

        return sanitized.ToString();
    }

    private static string SanitizeParameterValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Remove dangerous characters
        var sanitized = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (!DangerousCharacters.Contains(c))
            {
                sanitized.Append(c);
            }
        }

        // Escape remaining special characters for additional safety
        return sanitized.ToString()
            .Replace("\"", "\\\"")
            .Replace("'", "\\'");
    }

    private static bool IsSuspiciousEncoding(string encoded)
    {
        try
        {
            // Attempt to decode and check for dangerous patterns
            if (encoded.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                // Hex encoding check
                var hex = encoded[2..];
                if (hex.Length % 2 == 0 && hex.All(c => "0123456789ABCDEFabcdef".Contains(c)))
                {
                    var decoded = Convert.FromHexString(hex);
                    var decodedStr = Encoding.UTF8.GetString(decoded);
                    return ContainsDangerousPatterns(decodedStr);
                }
            }
            else if (IsBase64String(encoded))
            {
                // Base64 encoding check
                try
                {
                    var decoded = Convert.FromBase64String(encoded);
                    var decodedStr = Encoding.UTF8.GetString(decoded);
                    return ContainsDangerousPatterns(decodedStr);
                }
                catch
                {
                    // Not valid base64, could still be suspicious
                    return true;
                }
            }
        }
        catch
        {
            // If we can't decode it, treat as suspicious
            return true;
        }

        return false;
    }

    private static bool IsBase64String(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length % 4 != 0)
        {
            return false;
        }

        return s.All(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=');
    }

    private static bool ContainsDangerousPatterns(string text)
    {
        return DangerousPatterns.Any(pattern => pattern.IsMatch(text));
    }

    private static string GetContext(string text, int position, int contextLength = 20)
    {
        var start = Math.Max(0, position - contextLength);
        var end = Math.Min(text.Length, position + contextLength);
        var length = end - start;

        return text.Substring(start, length);
    }

    #endregion

    #region Compiled Regex Patterns

    // Command injection patterns
    [GeneratedRegex(@";(rm|del|format|dd|mkfs|:(){ :|:&};:|fork|shutdown|reboot)\s",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CommandInjectionRegex();

    [GeneratedRegex(@"\|(bash|sh|cmd|powershell|pwsh|python|perl|ruby|node)\s",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PipeInjectionRegex();

    [GeneratedRegex(@"\$\([^)]*\)", RegexOptions.Compiled)]
    private static partial Regex SubshellInjectionRegex();

    [GeneratedRegex(@"`[^`]*`", RegexOptions.Compiled)]
    private static partial Regex BacktickInjectionRegex();

    [GeneratedRegex(@"(&&|\|\|)\s*(rm|del|cat|curl|wget|nc|netcat)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex LogicalOperatorInjectionRegex();

    [GeneratedRegex(@"(>|>>|<)\s*(/dev/|/proc/|/sys/|[A-Za-z]:\\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RedirectionInjectionRegex();

    [GeneratedRegex(@"\$\{?[A-Za-z_][A-Za-z0-9_]*\}?", RegexOptions.Compiled)]
    private static partial Regex EnvironmentVariableInjectionRegex();

    [GeneratedRegex(@"<\([^)]*\)", RegexOptions.Compiled)]
    private static partial Regex ProcessSubstitutionRegex();

    [GeneratedRegex(@"<<-?\s*\w+", RegexOptions.Compiled)]
    private static partial Regex HereDocumentRegex();

    [GeneratedRegex(@"\$\(\(.*?\)\)", RegexOptions.Compiled)]
    private static partial Regex CommandSubstitutionRegex();

    // Encoding detection patterns
    [GeneratedRegex(@"[A-Za-z0-9+/]{20,}={0,2}", RegexOptions.Compiled)]
    private static partial Regex Base64EncodingRegex();

    [GeneratedRegex(@"(0x[0-9A-Fa-f]{2,}|\\x[0-9A-Fa-f]{2})", RegexOptions.Compiled)]
    private static partial Regex HexEncodingRegex();

    [GeneratedRegex(@"\\u[0-9A-Fa-f]{4}|\\U[0-9A-Fa-f]{8}", RegexOptions.Compiled)]
    private static partial Regex UnicodeEscapeRegex();

    [GeneratedRegex(@"%[0-9A-Fa-f]{2}", RegexOptions.Compiled)]
    private static partial Regex UrlEncodingRegex();

    [GeneratedRegex(@"&#?[a-zA-Z0-9]+;", RegexOptions.Compiled)]
    private static partial Regex HtmlEntityRegex();

    // Path traversal patterns
    [GeneratedRegex(@"\.\./", RegexOptions.Compiled)]
    private static partial Regex DotDotSlashRegex();

    [GeneratedRegex(@"\.\.\\", RegexOptions.Compiled)]
    private static partial Regex DotDotBackslashRegex();

    [GeneratedRegex(@"^/[a-zA-Z0-9_/.-]+", RegexOptions.Compiled)]
    private static partial Regex AbsoluteUnixPathRegex();

    [GeneratedRegex(@"^[A-Za-z]:\\", RegexOptions.Compiled)]
    private static partial Regex AbsoluteWindowsPathRegex();

    [GeneratedRegex(@"^~[/\\]", RegexOptions.Compiled)]
    private static partial Regex HomeDirRegex();

    [GeneratedRegex(@"^(\\\\|//)", RegexOptions.Compiled)]
    private static partial Regex UncPathRegex();

    // Parameter name validation
    [GeneratedRegex(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled)]
    private static partial Regex ParameterNameRegex();

    #endregion
}
