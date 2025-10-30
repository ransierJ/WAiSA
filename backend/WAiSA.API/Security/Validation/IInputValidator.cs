using WAiSA.API.Security.Validation.Models;

namespace WAiSA.API.Security.Validation;

/// <summary>
/// Interface for validating and sanitizing AI agent inputs to prevent injection attacks
/// </summary>
public interface IInputValidator
{
    /// <summary>
    /// Validates command syntax, length, and checks for dangerous patterns
    /// </summary>
    /// <param name="command">The command string to validate</param>
    /// <param name="parameters">Optional parameters dictionary</param>
    /// <returns>Validation result with detailed failure information</returns>
    ValidationResult ValidateCommand(string command, Dictionary<string, string>? parameters = null);

    /// <summary>
    /// Checks for command injection patterns and encoding attempts
    /// </summary>
    /// <param name="command">The command string to check</param>
    /// <returns>Validation result with pattern match details</returns>
    ValidationResult CheckForInjectionPatterns(string command);

    /// <summary>
    /// Sanitizes parameters by removing dangerous characters and validating names
    /// </summary>
    /// <param name="parameters">Parameters dictionary to sanitize</param>
    /// <returns>Sanitized parameters dictionary</returns>
    Dictionary<string, string> SanitizeParameters(Dictionary<string, string> parameters);

    /// <summary>
    /// Checks for path traversal attempts in parameters
    /// </summary>
    /// <param name="parameters">Parameters dictionary to check</param>
    /// <returns>List of path traversal violations</returns>
    IReadOnlyList<PathTraversalViolation> CheckForPathTraversal(Dictionary<string, string> parameters);
}
