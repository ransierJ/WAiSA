namespace WAiSA.API.Security.Validation.Models;

/// <summary>
/// Represents a path traversal violation detected in input
/// </summary>
public sealed class PathTraversalViolation
{
    /// <summary>
    /// Parameter name where violation was found
    /// </summary>
    public string ParameterName { get; init; }

    /// <summary>
    /// The value containing the violation
    /// </summary>
    public string Value { get; init; }

    /// <summary>
    /// Type of path traversal violation
    /// </summary>
    public PathTraversalType Type { get; init; }

    /// <summary>
    /// Detailed description of the violation
    /// </summary>
    public string Description { get; init; }

    public PathTraversalViolation(
        string parameterName,
        string value,
        PathTraversalType type,
        string description)
    {
        ParameterName = parameterName;
        Value = value;
        Type = type;
        Description = description;
    }
}

/// <summary>
/// Types of path traversal attempts
/// </summary>
public enum PathTraversalType
{
    DotDotSequence,
    AbsolutePath,
    HomeDirectory,
    WindowsPath,
    UncPath
}
