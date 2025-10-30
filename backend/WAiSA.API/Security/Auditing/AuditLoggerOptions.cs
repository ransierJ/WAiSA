namespace WAiSA.API.Security.Auditing;

/// <summary>
/// Configuration options for the audit logger
/// </summary>
public class AuditLoggerOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "AuditLogging";

    /// <summary>
    /// Enable file-based logging
    /// </summary>
    public bool EnableFileLogging { get; set; } = true;

    /// <summary>
    /// Base directory for audit log files
    /// </summary>
    public string LogDirectory { get; set; } =
        OperatingSystem.IsWindows()
            ? @"C:\Logs\AgentAudit"
            : "/var/log/agent-audit";

    /// <summary>
    /// Enable Application Insights logging
    /// </summary>
    public bool EnableApplicationInsights { get; set; } = false;

    /// <summary>
    /// Number of days to keep uncompressed log files
    /// </summary>
    public int LogRetentionDays { get; set; } = 7;

    /// <summary>
    /// Number of days to keep compressed log files
    /// </summary>
    public int CompressedLogRetentionDays { get; set; } = 90;

    /// <summary>
    /// Maximum log file size in MB before rotation
    /// </summary>
    public int MaxLogFileSizeMb { get; set; } = 100;

    /// <summary>
    /// Enable automatic compression of old log files
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Include stack traces in error logs
    /// </summary>
    public bool IncludeStackTraces { get; set; } = true;

    /// <summary>
    /// Buffer size for batch writing (0 = write immediately)
    /// </summary>
    public int BufferSize { get; set; } = 0;
}
