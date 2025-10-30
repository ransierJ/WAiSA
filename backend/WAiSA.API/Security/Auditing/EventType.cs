namespace WAiSA.API.Security.Auditing;

/// <summary>
/// Represents the type of audit event being logged
/// </summary>
public enum EventType
{
    /// <summary>
    /// Agent executed a command or action
    /// </summary>
    CommandExecution,

    /// <summary>
    /// Agent accessed data or resources
    /// </summary>
    DataAccess,

    /// <summary>
    /// Security policy violation detected
    /// </summary>
    SecurityViolation,

    /// <summary>
    /// Authentication event occurred
    /// </summary>
    Authentication,

    /// <summary>
    /// Authorization decision was made
    /// </summary>
    Authorization,

    /// <summary>
    /// Configuration was changed
    /// </summary>
    ConfigurationChange,

    /// <summary>
    /// Error or exception occurred
    /// </summary>
    Error,

    /// <summary>
    /// Agent session started
    /// </summary>
    SessionStart,

    /// <summary>
    /// Agent session ended
    /// </summary>
    SessionEnd,

    /// <summary>
    /// Resource was created
    /// </summary>
    ResourceCreated,

    /// <summary>
    /// Resource was modified
    /// </summary>
    ResourceModified,

    /// <summary>
    /// Resource was deleted
    /// </summary>
    ResourceDeleted,

    /// <summary>
    /// API call was made
    /// </summary>
    ApiCall,

    /// <summary>
    /// Other audit event type
    /// </summary>
    Other
}
