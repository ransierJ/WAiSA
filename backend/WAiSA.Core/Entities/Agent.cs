namespace WAiSA.Core.Entities;

/// <summary>
/// Represents a Windows Agent installed on an endpoint
/// </summary>
public class Agent
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique agent identifier (GUID)
    /// </summary>
    public Guid AgentId { get; set; }

    /// <summary>
    /// Computer/hostname where agent is installed
    /// </summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>
    /// Hashed API key for authentication
    /// </summary>
    public string ApiKeyHash { get; set; } = string.Empty;

    /// <summary>
    /// Current agent status
    /// </summary>
    public AgentStatus Status { get; set; } = AgentStatus.Offline;

    /// <summary>
    /// Last heartbeat received from agent
    /// </summary>
    public DateTime? LastHeartbeat { get; set; }

    /// <summary>
    /// Last collected system information (JSON)
    /// </summary>
    public string? LastSystemInfo { get; set; }

    /// <summary>
    /// When the agent was installed/registered
    /// </summary>
    public DateTime InstallDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Agent software version
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Operating system version
    /// </summary>
    public string? OsVersion { get; set; }

    /// <summary>
    /// Installation key used to register (for auditing)
    /// </summary>
    public string? InstallationKey { get; set; }

    /// <summary>
    /// Whether the agent is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Navigation property: Commands queued for this agent
    /// </summary>
    public ICollection<CommandQueue> Commands { get; set; } = new List<CommandQueue>();
}

/// <summary>
/// Agent status enumeration
/// </summary>
public enum AgentStatus
{
    Online,
    Offline,
    Error,
    Disabled
}
