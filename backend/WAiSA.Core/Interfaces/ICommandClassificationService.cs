namespace WAiSA.Core.Interfaces;

/// <summary>
/// Service for classifying PowerShell commands as read-only or requiring approval
/// </summary>
public interface ICommandClassificationService
{
    /// <summary>
    /// Classify a command and determine if it requires human approval
    /// </summary>
    /// <param name="command">PowerShell command to classify</param>
    /// <returns>Classification result with risk level and reasoning</returns>
    CommandClassification ClassifyCommand(string command);

    /// <summary>
    /// Extract PowerShell commands from AI response text
    /// </summary>
    /// <param name="aiResponse">AI response text</param>
    /// <returns>List of extracted commands</returns>
    List<string> ExtractCommandsFromResponse(string aiResponse);
}

/// <summary>
/// Result of command classification
/// </summary>
public class CommandClassification
{
    /// <summary>
    /// Whether command requires human approval before execution
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// Risk level: Low, Medium, High, Critical
    /// </summary>
    public RiskLevel RiskLevel { get; set; }

    /// <summary>
    /// Category of operation
    /// </summary>
    public CommandCategory Category { get; set; }

    /// <summary>
    /// Reasoning for classification
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Suggested timeout in seconds
    /// </summary>
    public int SuggestedTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Whether this is a destructive operation
    /// </summary>
    public bool IsDestructive { get; set; }
}

public enum RiskLevel
{
    Low,      // Read-only operations, safe queries
    Medium,   // Configuration changes, service restarts
    High,     // File modifications, user management
    Critical  // System-wide changes, deletions, security modifications
}

public enum CommandCategory
{
    Query,              // Get-, Test-, Select-Object, Where-Object
    ServiceManagement,  // Start-Service, Stop-Service, Restart-Service
    ProcessManagement,  // Get-Process, Stop-Process
    FileOperation,      // Copy-Item, Move-Item, New-Item, Remove-Item
    UserManagement,     // New-LocalUser, Set-LocalUser, Remove-LocalUser
    NetworkConfig,      // Set-NetIPAddress, Set-DnsClientServerAddress
    SecurityPolicy,     // Set-ExecutionPolicy, Enable-PSRemoting
    SystemConfig,       // Set-ItemProperty (registry), Set-Service
    SoftwareManagement, // Install-Package, Uninstall-Package
    Unknown             // Unrecognized or complex commands
}
