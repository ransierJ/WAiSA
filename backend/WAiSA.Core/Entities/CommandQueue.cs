namespace WAiSA.Core.Entities;

/// <summary>
/// Represents a command queued for execution on an agent
/// </summary>
public class CommandQueue
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique command identifier (GUID)
    /// </summary>
    public Guid CommandId { get; set; }

    /// <summary>
    /// Agent ID (foreign key)
    /// </summary>
    public Guid AgentId { get; set; }

    /// <summary>
    /// PowerShell command to execute
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Context/reason for execution (for logging/auditing)
    /// </summary>
    public string? ExecutionContext { get; set; }

    /// <summary>
    /// Command execution status
    /// </summary>
    public CommandStatus Status { get; set; } = CommandStatus.Pending;

    /// <summary>
    /// When the command was created/queued
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the command execution started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the command execution completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Command output (stdout)
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Command error output (stderr)
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Execution time in seconds
    /// </summary>
    public double? ExecutionTimeSeconds { get; set; }

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Whether execution was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// User or system that initiated the command
    /// </summary>
    public string? InitiatedBy { get; set; }

    /// <summary>
    /// Related chat session ID (if from chat)
    /// </summary>
    public string? ChatSessionId { get; set; }

    /// <summary>
    /// Whether this command requires human approval
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// Whether the command has been approved
    /// </summary>
    public bool? Approved { get; set; }

    /// <summary>
    /// Who approved the command
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// When the command was approved
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Navigation property: Associated agent
    /// </summary>
    public Agent? Agent { get; set; }
}

/// <summary>
/// Command execution status enumeration
/// </summary>
public enum CommandStatus
{
    Pending,
    Approved,
    Executing,
    Completed,
    Failed,
    TimedOut,
    Cancelled
}
