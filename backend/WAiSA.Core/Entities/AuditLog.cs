namespace WAiSA.Core.Entities;

/// <summary>
/// Audit log entity - stored in SQL Database
/// Tracks all system actions for compliance and troubleshooting
/// </summary>
public class AuditLog
{
    /// <summary>
    /// Primary key
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Device ID (optional - system-level actions may not have device)
    /// </summary>
    public int? DeviceId { get; set; }

    /// <summary>
    /// Navigation property to Device
    /// </summary>
    public Device? Device { get; set; }

    /// <summary>
    /// Action category (e.g., "Command", "Query", "Configuration", "Error")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Action type (e.g., "PowerShellExecution", "FileAccess", "AIQuery")
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Action description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// User or system actor
    /// </summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>
    /// Severity level (Info, Warning, Error, Critical)
    /// </summary>
    public string Severity { get; set; } = "Info";

    /// <summary>
    /// Whether action was successful
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if action failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Source IP address
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Request correlation ID
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Timestamp of the action
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
