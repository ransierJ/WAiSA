namespace WAiSA.Core.Entities;

/// <summary>
/// System configuration entity - stored in SQL Database
/// Stores application-level settings and feature flags
/// </summary>
public class SystemConfiguration
{
    /// <summary>
    /// Primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Configuration key (unique)
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Configuration value
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Configuration description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Configuration category (e.g., "AI", "Security", "Performance")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Value data type (string, int, bool, json)
    /// </summary>
    public string DataType { get; set; } = "string";

    /// <summary>
    /// Whether this setting is encrypted
    /// </summary>
    public bool IsEncrypted { get; set; } = false;

    /// <summary>
    /// Whether this setting is a feature flag
    /// </summary>
    public bool IsFeatureFlag { get; set; } = false;

    /// <summary>
    /// Whether this setting is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last modified by
    /// </summary>
    public string ModifiedBy { get; set; } = "System";

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
