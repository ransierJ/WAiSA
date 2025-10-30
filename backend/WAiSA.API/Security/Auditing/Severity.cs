namespace WAiSA.API.Security.Auditing;

/// <summary>
/// Represents the severity level of an audit event
/// </summary>
public enum Severity
{
    /// <summary>
    /// Informational event, normal operation
    /// </summary>
    Info,

    /// <summary>
    /// Warning event, potential issue
    /// </summary>
    Warning,

    /// <summary>
    /// High severity event, requires attention
    /// </summary>
    High,

    /// <summary>
    /// Critical event, immediate action required
    /// </summary>
    Critical
}
