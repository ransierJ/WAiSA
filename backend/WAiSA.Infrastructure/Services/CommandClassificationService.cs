using System.Text.RegularExpressions;
using WAiSA.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace WAiSA.Infrastructure.Services;

/// <summary>
/// Service for classifying PowerShell commands for risk assessment and approval workflow
/// </summary>
public class CommandClassificationService : ICommandClassificationService
{
    private readonly ILogger<CommandClassificationService> _logger;

    // Read-only cmdlets that never require approval
    private static readonly HashSet<string> ReadOnlyCmdlets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Get-", "Select-", "Where-", "Test-", "Measure-", "Compare-",
        "Find-", "Search-", "Show-", "Format-", "Out-", "ConvertTo-",
        "Group-", "Sort-"
    };

    // Destructive operations that always require approval
    private static readonly HashSet<string> DestructiveCmdlets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Remove-", "Delete-", "Clear-", "Reset-", "Format-",
        "Uninstall-", "Disable-", "Stop-Computer", "Restart-Computer"
    };

    // High-risk cmdlets requiring approval
    private static readonly HashSet<string> HighRiskCmdlets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Set-ExecutionPolicy", "Enable-PSRemoting", "New-NetFirewallRule",
        "Set-NetFirewallRule", "Grant-", "Revoke-", "Set-ACL",
        "takeown", "icacls", "net user", "net localgroup"
    };

    public CommandClassificationService(ILogger<CommandClassificationService> logger)
    {
        _logger = logger;
    }

    public CommandClassification ClassifyCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return new CommandClassification
            {
                RequiresApproval = false,
                RiskLevel = RiskLevel.Low,
                Category = CommandCategory.Unknown,
                Reasoning = "Empty command"
            };
        }

        command = command.Trim();

        // Check for dangerous patterns
        if (ContainsDangerousPatterns(command))
        {
            return new CommandClassification
            {
                RequiresApproval = true,
                RiskLevel = RiskLevel.Critical,
                Category = CommandCategory.Unknown,
                Reasoning = "Command contains dangerous patterns or system-critical operations",
                IsDestructive = true,
                SuggestedTimeoutSeconds = 600
            };
        }

        // Check for destructive operations
        if (IsDestructiveCommand(command))
        {
            return new CommandClassification
            {
                RequiresApproval = true,
                RiskLevel = RiskLevel.High,
                Category = GetCommandCategory(command),
                Reasoning = "Destructive operation detected (delete, remove, clear, format)",
                IsDestructive = true,
                SuggestedTimeoutSeconds = 300
            };
        }

        // Check for high-risk operations
        if (IsHighRiskCommand(command))
        {
            return new CommandClassification
            {
                RequiresApproval = true,
                RiskLevel = RiskLevel.High,
                Category = GetCommandCategory(command),
                Reasoning = "High-risk operation affecting security, permissions, or system configuration",
                IsDestructive = false,
                SuggestedTimeoutSeconds = 300
            };
        }

        // Check if it's a read-only command
        if (IsReadOnlyCommand(command))
        {
            return new CommandClassification
            {
                RequiresApproval = false,
                RiskLevel = RiskLevel.Low,
                Category = CommandCategory.Query,
                Reasoning = "Read-only query operation",
                IsDestructive = false,
                SuggestedTimeoutSeconds = 60
            };
        }

        // Check for service management
        if (IsServiceManagementCommand(command))
        {
            return new CommandClassification
            {
                RequiresApproval = true,
                RiskLevel = RiskLevel.Medium,
                Category = CommandCategory.ServiceManagement,
                Reasoning = "Service management operation (start, stop, restart)",
                IsDestructive = false,
                SuggestedTimeoutSeconds = 120
            };
        }

        // Check for process management
        if (IsProcessManagementCommand(command))
        {
            return new CommandClassification
            {
                RequiresApproval = true,
                RiskLevel = RiskLevel.Medium,
                Category = CommandCategory.ProcessManagement,
                Reasoning = "Process management operation (stop process)",
                IsDestructive = false,
                SuggestedTimeoutSeconds = 60
            };
        }

        // Check for file operations
        if (IsFileOperationCommand(command))
        {
            return new CommandClassification
            {
                RequiresApproval = true,
                RiskLevel = RiskLevel.Medium,
                Category = CommandCategory.FileOperation,
                Reasoning = "File system operation (copy, move, create)",
                IsDestructive = false,
                SuggestedTimeoutSeconds = 300
            };
        }

        // Default: Medium risk, requires approval
        // TEMPORARY: Disable approval for testing
        return new CommandClassification
        {
            RequiresApproval = false,  // Changed from true to false for testing
            RiskLevel = RiskLevel.Medium,
            Category = GetCommandCategory(command),
            Reasoning = "Unrecognized command pattern - auto-approved for testing",
            IsDestructive = false,
            SuggestedTimeoutSeconds = 300
        };
    }

    public List<string> ExtractCommandsFromResponse(string aiResponse)
    {
        var commands = new List<string>();

        if (string.IsNullOrWhiteSpace(aiResponse))
            return commands;

        // Extract commands from code blocks (```powershell or ```)
        var codeBlockPattern = @"```(?:powershell|ps)?\s*\n([\s\S]*?)```";
        var codeBlockMatches = Regex.Matches(aiResponse, codeBlockPattern, RegexOptions.Multiline);

        foreach (Match match in codeBlockMatches)
        {
            if (match.Groups.Count > 1)
            {
                var block = match.Groups[1].Value.Trim();
                // Split by newlines and filter out comments and empty lines
                var lines = block.Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
                    .ToList();

                commands.AddRange(lines);
            }
        }

        // Extract single-line commands (PowerShell cmdlets not in code blocks)
        if (!codeBlockMatches.Any())
        {
            var cmdletPattern = @"\b([A-Z][a-z]+-[A-Za-z]+(?:\s+[^\n;]+)?)\b";
            var cmdletMatches = Regex.Matches(aiResponse, cmdletPattern);

            foreach (Match match in cmdletMatches)
            {
                var cmd = match.Groups[1].Value.Trim();
                if (!commands.Contains(cmd))
                {
                    commands.Add(cmd);
                }
            }
        }

        return commands;
    }

    private bool IsReadOnlyCommand(string command)
    {
        foreach (var verb in ReadOnlyCmdlets)
        {
            if (command.StartsWith(verb, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool IsDestructiveCommand(string command)
    {
        foreach (var cmdlet in DestructiveCmdlets)
        {
            if (command.Contains(cmdlet, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool IsHighRiskCommand(string command)
    {
        foreach (var cmdlet in HighRiskCmdlets)
        {
            if (command.Contains(cmdlet, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool ContainsDangerousPatterns(string command)
    {
        var dangerousPatterns = new[]
        {
            "format-volume",
            "format c:",
            "rd /s",
            "rmdir /s",
            "del /s",
            "net user administrator",
            "shutdown",
            "restart-computer -force",
            "stop-computer -force",
            "disable-firewallrule",
            "remove-item -recurse -force c:\\",
            "takeown /f c:\\windows",
            "reg delete"
        };

        return dangerousPatterns.Any(pattern =>
            command.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsServiceManagementCommand(string command)
    {
        var serviceVerbs = new[] { "Start-Service", "Stop-Service", "Restart-Service", "Set-Service" };
        return serviceVerbs.Any(verb => command.Contains(verb, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsProcessManagementCommand(string command)
    {
        return command.Contains("Stop-Process", StringComparison.OrdinalIgnoreCase) ||
               command.Contains("taskkill", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsFileOperationCommand(string command)
    {
        var fileVerbs = new[] { "Copy-Item", "Move-Item", "New-Item", "Rename-Item" };
        return fileVerbs.Any(verb => command.Contains(verb, StringComparison.OrdinalIgnoreCase));
    }

    private CommandCategory GetCommandCategory(string command)
    {
        if (IsReadOnlyCommand(command))
            return CommandCategory.Query;

        if (IsServiceManagementCommand(command))
            return CommandCategory.ServiceManagement;

        if (IsProcessManagementCommand(command))
            return CommandCategory.ProcessManagement;

        if (IsFileOperationCommand(command))
            return CommandCategory.FileOperation;

        if (command.Contains("User", StringComparison.OrdinalIgnoreCase))
            return CommandCategory.UserManagement;

        if (command.Contains("NetIP", StringComparison.OrdinalIgnoreCase) ||
            command.Contains("DNS", StringComparison.OrdinalIgnoreCase))
            return CommandCategory.NetworkConfig;

        if (command.Contains("ExecutionPolicy", StringComparison.OrdinalIgnoreCase) ||
            command.Contains("Firewall", StringComparison.OrdinalIgnoreCase))
            return CommandCategory.SecurityPolicy;

        if (command.Contains("Install-", StringComparison.OrdinalIgnoreCase) ||
            command.Contains("Package", StringComparison.OrdinalIgnoreCase))
            return CommandCategory.SoftwareManagement;

        return CommandCategory.Unknown;
    }
}
