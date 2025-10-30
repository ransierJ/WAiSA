using WAiSA.API.Security.Models;
using System.Text.RegularExpressions;

namespace WAiSA.API.Security.Filtering;

/// <summary>
/// Semantic analyzer for detecting malicious intent in commands
/// Uses pattern matching and heuristics to identify dangerous operations
/// </summary>
public sealed class SemanticAnalyzer : ISemanticAnalyzer
{
    private readonly ILogger<SemanticAnalyzer> _logger;

    // Patterns indicating potential security threats
    private readonly Dictionary<string, Regex> _threatPatterns;

    public SemanticAnalyzer(ILogger<SemanticAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize threat detection patterns
        _threatPatterns = new Dictionary<string, Regex>
        {
            // Privilege escalation indicators
            ["privilege_escalation"] = new Regex(
                @"(sudo|runas|elevation|administrator|admin\s+rights|UAC\s+bypass)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Lateral movement indicators
            ["lateral_movement"] = new Regex(
                @"(-ComputerName\s+(?!localhost|127\.0\.0\.1|\.))|" +
                @"(Invoke-Command.*-ComputerName)|" +
                @"(Enter-PSSession.*-ComputerName)|" +
                @"(ssh\s+\w+@)|" +
                @"(winrs\s+-r:)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Data exfiltration indicators
            ["data_exfiltration"] = new Regex(
                @"(Invoke-WebRequest.*-Method\s+POST)|" +
                @"(curl.*--data)|" +
                @"(wget.*--post-data)|" +
                @"(Send-MailMessage.*-Attachments)|" +
                @"(Start-BitsTransfer.*http)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Credential theft indicators
            ["credential_theft"] = new Regex(
                @"(mimikatz|sekurlsa|lsadump|procdump.*lsass)|" +
                @"(Get-Credential\s*\||ConvertFrom-SecureString)|" +
                @"(registry.*sam|registry.*security)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Obfuscation indicators
            ["obfuscation"] = new Regex(
                @"(base64\s*--decode|FromBase64String)|" +
                @"(-enc\s+[A-Za-z0-9+/=]{20,})|" +
                @"(char\[\].*join)|" +
                @"(\[char\]\d+\s*\+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Persistence mechanisms
            ["persistence"] = new Regex(
                @"(schtasks.*\/create|New-ScheduledTask)|" +
                @"(HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run)|" +
                @"(startup.*\.lnk|startup.*\.bat)|" +
                @"(WMI.*EventConsumer)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Destructive operations
            ["destructive"] = new Regex(
                @"(Format-Volume|Clear-Disk)|" +
                @"(Remove-Item.*-Recurse.*-Force)|" +
                @"(rd\s+\/s\s+\/q)|" +
                @"(del.*\/f.*\/s.*\/q)|" +
                @"(Drop\s+Database|Truncate\s+Table)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Remote code execution
            ["remote_execution"] = new Regex(
                @"(Invoke-Expression.*http)|" +
                @"(IEX.*DownloadString)|" +
                @"(Start-Process.*http)|" +
                @"(\|\s*iex|\|\s*Invoke-Expression)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
    }

    /// <summary>
    /// Analyze command for malicious intent using semantic patterns
    /// </summary>
    public async Task<SemanticAnalysisResult> AnalyzeCommandAsync(
        string command,
        Dictionary<string, string>? parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        await Task.CompletedTask; // Placeholder for async operations

        // Check each threat pattern
        foreach (var (threatType, pattern) in _threatPatterns)
        {
            if (pattern.IsMatch(command))
            {
                _logger.LogWarning(
                    "Semantic analysis detected {ThreatType} pattern in command: {Command}",
                    threatType,
                    command);

                return new SemanticAnalysisResult(
                    false,
                    $"Command contains {threatType.Replace('_', ' ')} indicators");
            }
        }

        // Analyze parameter values for threats
        if (parameters is not null)
        {
            foreach (var param in parameters)
            {
                foreach (var (threatType, pattern) in _threatPatterns)
                {
                    if (pattern.IsMatch(param.Value))
                    {
                        _logger.LogWarning(
                            "Semantic analysis detected {ThreatType} pattern in parameter {ParameterName}",
                            threatType,
                            param.Key);

                        return new SemanticAnalysisResult(
                            false,
                            $"Parameter '{param.Key}' contains {threatType.Replace('_', ' ')} indicators");
                    }
                }
            }
        }

        // Check for suspicious command combinations
        var suspiciousCombinations = DetectSuspiciousCombinations(command);
        if (suspiciousCombinations is not null)
        {
            return new SemanticAnalysisResult(false, suspiciousCombinations);
        }

        _logger.LogDebug("Semantic analysis passed for command: {Command}", command);
        return new SemanticAnalysisResult(true, "No semantic threats detected");
    }

    /// <summary>
    /// Detect suspicious command combinations that might indicate attack chains
    /// </summary>
    private string? DetectSuspiciousCombinations(string command)
    {
        // Download + Execute pattern
        if (command.Contains("DownloadString", StringComparison.OrdinalIgnoreCase) &&
            command.Contains("Invoke-Expression", StringComparison.OrdinalIgnoreCase))
        {
            return "Detected download-and-execute pattern";
        }

        // Credential dump + Send pattern
        if ((command.Contains("Get-Credential", StringComparison.OrdinalIgnoreCase) ||
             command.Contains("ConvertFrom-SecureString", StringComparison.OrdinalIgnoreCase)) &&
            (command.Contains("Send-Mail", StringComparison.OrdinalIgnoreCase) ||
             command.Contains("Invoke-WebRequest", StringComparison.OrdinalIgnoreCase)))
        {
            return "Detected credential exfiltration pattern";
        }

        // Disable security + Execute pattern
        if ((command.Contains("Set-ExecutionPolicy", StringComparison.OrdinalIgnoreCase) ||
             command.Contains("Disable-", StringComparison.OrdinalIgnoreCase)) &&
            command.Contains("Invoke-", StringComparison.OrdinalIgnoreCase))
        {
            return "Detected security bypass pattern";
        }

        return null;
    }
}
