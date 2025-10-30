namespace WAiSA.Core.Entities;

/// <summary>
/// Device registration entity - stored in SQL Database
/// Represents structured metadata for registered Windows devices
/// </summary>
public class Device
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique device identifier (matches Cosmos DB DeviceId)
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Device friendly name
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Hostname
    /// </summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// Operating System (e.g., "Windows 11 Pro")
    /// </summary>
    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>
    /// OS Version (e.g., "10.0.22631")
    /// </summary>
    public string OSVersion { get; set; } = string.Empty;

    /// <summary>
    /// Device manufacturer
    /// </summary>
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>
    /// Device model
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// CPU architecture (e.g., "x64")
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// IP address
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// MAC address
    /// </summary>
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>
    /// Whether device is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last heartbeat timestamp
    /// </summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// First registration timestamp
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property - audit logs for this device
    /// </summary>
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
