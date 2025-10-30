namespace WAiSA.API.Security.Validation.Models;

/// <summary>
/// Represents a single validation failure
/// </summary>
public sealed class ValidationFailure
{
    /// <summary>
    /// Type of validation failure
    /// </summary>
    public ValidationFailureType Type { get; init; }

    /// <summary>
    /// Human-readable failure message
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Pattern that was matched (if applicable)
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Severity level of this failure
    /// </summary>
    public ValidationSeverity Severity { get; init; }

    /// <summary>
    /// Location or context of the failure
    /// </summary>
    public string? Context { get; init; }

    public ValidationFailure(
        ValidationFailureType type,
        string message,
        ValidationSeverity severity = ValidationSeverity.High,
        string? pattern = null,
        string? context = null)
    {
        Type = type;
        Message = message;
        Severity = severity;
        Pattern = pattern;
        Context = context;
    }
}

/// <summary>
/// Types of validation failures
/// </summary>
public enum ValidationFailureType
{
    SyntaxError,
    LengthExceeded,
    CommandInjection,
    PathTraversal,
    EncodingAttempt,
    NullByteDetected,
    InvalidCharacter,
    InvalidParameterName,
    DangerousPattern
}

/// <summary>
/// Severity levels for validation failures
/// </summary>
public enum ValidationSeverity
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
