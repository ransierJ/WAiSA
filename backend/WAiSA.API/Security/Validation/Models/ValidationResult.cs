namespace WAiSA.API.Security.Validation.Models;

/// <summary>
/// Represents the result of an input validation operation
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Indicates whether the validation passed
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Collection of validation failures
    /// </summary>
    public IReadOnlyList<ValidationFailure> Failures { get; init; }

    /// <summary>
    /// Overall severity level of the validation result
    /// </summary>
    public ValidationSeverity Severity { get; init; }

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public ValidationResult()
    {
        IsValid = true;
        Failures = Array.Empty<ValidationFailure>();
        Severity = ValidationSeverity.None;
    }

    /// <summary>
    /// Creates a validation result with failures
    /// </summary>
    public ValidationResult(IEnumerable<ValidationFailure> failures)
    {
        var failureList = failures.ToList();
        IsValid = failureList.Count == 0;
        Failures = failureList.AsReadOnly();
        Severity = failureList.Any()
            ? failureList.Max(f => f.Severity)
            : ValidationSeverity.None;
    }

    /// <summary>
    /// Creates a validation result with a single failure
    /// </summary>
    public static ValidationResult Fail(ValidationFailure failure)
    {
        return new ValidationResult(new[] { failure });
    }

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public static ValidationResult Success()
    {
        return new ValidationResult();
    }
}
