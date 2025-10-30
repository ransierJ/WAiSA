using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WAiSA.API.Security.Auditing;

/// <summary>
/// Provides comprehensive audit logging for AI agent actions
/// </summary>
public class AuditLogger : IAuditLogger, IDisposable
{
    private readonly ILogger<AuditLogger> _logger;
    private readonly AuditLoggerOptions _options;
    private readonly TelemetryClient? _telemetryClient;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly HashSet<string> _sensitiveKeys;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    // Regex patterns for detecting sensitive data
    private static readonly Regex SensitiveKeyPattern = new(
        @"(password|secret|apikey|api_key|token|credential|connectionstring|connection_string|auth|authorization|bearer)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public AuditLogger(
        ILogger<AuditLogger> logger,
        IOptions<AuditLoggerOptions> options,
        TelemetryClient? telemetryClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _telemetryClient = telemetryClient;

        _sensitiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "secret", "apikey", "api_key", "token", "credential",
            "connectionstring", "connection_string", "auth", "authorization",
            "bearer", "accesstoken", "access_token", "refreshtoken", "refresh_token",
            "clientsecret", "client_secret", "privatekey", "private_key"
        };

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        EnsureLogDirectoryExists();
        StartBackgroundCleanup();
    }

    /// <inheritdoc/>
    public async Task LogAgentActionAsync(
        AgentActionEvent actionEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actionEvent);

        try
        {
            // Create audit log entry
            var logEntry = CreateAuditLogEntry(actionEvent);

            // Log to multiple destinations in parallel
            var tasks = new List<Task>();

            if (_options.EnableFileLogging)
            {
                tasks.Add(LogToJsonFileAsync(logEntry, cancellationToken));
            }

            if (_options.EnableApplicationInsights && _telemetryClient != null)
            {
                tasks.Add(LogToApplicationInsightsAsync(logEntry, cancellationToken));
            }

            await Task.WhenAll(tasks);

            _logger.LogDebug(
                "Audit log entry created: EventId={EventId}, AgentId={AgentId}, EventType={EventType}",
                logEntry.EventId,
                logEntry.AgentId,
                logEntry.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log agent action: {Error}", ex.Message);
            // Don't throw - audit logging should not break application flow
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<AuditLogEntry>> QueryLogsAsync(
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string? agentId = null,
        string? userId = null,
        EventType? eventType = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<AuditLogEntry>();

        try
        {
            var logFiles = GetLogFilesInDateRange(startDate, endDate);

            foreach (var logFile in logFiles)
            {
                await foreach (var entry in ReadLogEntriesAsync(logFile, cancellationToken))
                {
                    if (entry.Timestamp < startDate || entry.Timestamp > endDate)
                        continue;

                    if (agentId != null && !entry.AgentId.Equals(agentId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (userId != null && !entry.UserId?.Equals(userId, StringComparison.OrdinalIgnoreCase) == true)
                        continue;

                    if (eventType.HasValue && !entry.EventType.Equals(eventType.ToString(), StringComparison.OrdinalIgnoreCase))
                        continue;

                    results.Add(entry);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query audit logs: {Error}", ex.Message);
            throw;
        }

        return results;
    }

    /// <inheritdoc/>
    public bool VerifyIntegrity(AuditLogEntry logEntry)
    {
        ArgumentNullException.ThrowIfNull(logEntry);

        try
        {
            var calculatedHash = CalculateIntegrityHash(logEntry);
            return calculatedHash.Equals(logEntry.IntegrityHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify log entry integrity: {Error}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Creates an audit log entry from an agent action event
    /// </summary>
    private AuditLogEntry CreateAuditLogEntry(AgentActionEvent actionEvent)
    {
        var eventId = Guid.NewGuid().ToString("N");
        var timestamp = DateTimeOffset.UtcNow;

        var sanitizedParameters = actionEvent.Parameters != null
            ? SanitizeParameters(actionEvent.Parameters)
            : null;

        var logEntry = new AuditLogEntry
        {
            Timestamp = timestamp,
            EventId = eventId,
            AgentId = actionEvent.AgentId,
            SessionId = actionEvent.SessionId,
            UserId = actionEvent.UserId,
            EventType = actionEvent.EventType.ToString(),
            Severity = actionEvent.Severity.ToString(),
            EventData = new EventData
            {
                Command = actionEvent.Command,
                Parameters = sanitizedParameters,
                Result = actionEvent.Result,
                ExecutionTimeMs = actionEvent.ExecutionTimeMs,
                ErrorMessage = actionEvent.ErrorMessage,
                StackTrace = _options.IncludeStackTraces ? actionEvent.StackTrace : null
            },
            SecurityContext = new SecurityContext
            {
                SourceIpAddress = actionEvent.SourceIpAddress,
                AuthenticationMethod = actionEvent.AuthenticationMethod,
                AuthorizationDecision = actionEvent.AuthorizationDecision
            },
            ResourceContext = (actionEvent.SubscriptionId != null ||
                             actionEvent.ResourceGroup != null ||
                             actionEvent.ResourceId != null)
                ? new ResourceContext
                {
                    SubscriptionId = actionEvent.SubscriptionId,
                    ResourceGroup = actionEvent.ResourceGroup,
                    ResourceId = actionEvent.ResourceId
                }
                : null,
            Metadata = actionEvent.Metadata,
            IntegrityHash = string.Empty // Will be calculated next
        };

        // Calculate integrity hash
        logEntry.IntegrityHash = CalculateIntegrityHash(logEntry);

        return logEntry;
    }

    /// <summary>
    /// Sanitizes parameters to remove sensitive information
    /// </summary>
    private Dictionary<string, object> SanitizeParameters(Dictionary<string, object> parameters)
    {
        var sanitized = new Dictionary<string, object>(parameters.Count);

        foreach (var kvp in parameters)
        {
            if (IsSensitiveKey(kvp.Key))
            {
                sanitized[kvp.Key] = "***REDACTED***";
            }
            else if (kvp.Value is string stringValue && ContainsSensitiveData(stringValue))
            {
                sanitized[kvp.Key] = "***REDACTED***";
            }
            else if (kvp.Value is Dictionary<string, object> nestedDict)
            {
                sanitized[kvp.Key] = SanitizeParameters(nestedDict);
            }
            else
            {
                sanitized[kvp.Key] = kvp.Value;
            }
        }

        return sanitized;
    }

    /// <summary>
    /// Checks if a key name indicates sensitive data
    /// </summary>
    private bool IsSensitiveKey(string key)
    {
        return _sensitiveKeys.Contains(key) || SensitiveKeyPattern.IsMatch(key);
    }

    /// <summary>
    /// Checks if a string value contains sensitive data patterns
    /// </summary>
    private bool ContainsSensitiveData(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Check for common sensitive data patterns
        return value.Length > 20 && (
            value.Contains("Bearer ", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Basic ", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("eyJ", StringComparison.Ordinal) || // JWT
            Regex.IsMatch(value, @"^[A-Za-z0-9+/]{40,}={0,2}$") // Base64
        );
    }

    /// <summary>
    /// Calculates SHA256 integrity hash for the log entry
    /// </summary>
    private string CalculateIntegrityHash(AuditLogEntry logEntry)
    {
        // Create a copy without the hash field for integrity calculation
        var entryForHashing = new AuditLogEntry
        {
            Timestamp = logEntry.Timestamp,
            EventId = logEntry.EventId,
            AgentId = logEntry.AgentId,
            SessionId = logEntry.SessionId,
            UserId = logEntry.UserId,
            EventType = logEntry.EventType,
            Severity = logEntry.Severity,
            EventData = logEntry.EventData,
            SecurityContext = logEntry.SecurityContext,
            ResourceContext = logEntry.ResourceContext,
            Metadata = logEntry.Metadata,
            IntegrityHash = string.Empty  // Exclude hash from hash calculation
        };

        var json = JsonSerializer.Serialize(entryForHashing, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Writes audit log entry to JSON file
    /// </summary>
    private async Task LogToJsonFileAsync(AuditLogEntry logEntry, CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken);

        try
        {
            var logFilePath = GetLogFilePath(logEntry.Timestamp);
            var json = JsonSerializer.Serialize(logEntry, _jsonOptions);

            await File.AppendAllTextAsync(logFilePath, json + Environment.NewLine, cancellationToken);

            _logger.LogTrace("Audit log written to file: {FilePath}", logFilePath);

            // Check if rotation is needed
            var fileInfo = new FileInfo(logFilePath);
            if (fileInfo.Exists && fileInfo.Length > _options.MaxLogFileSizeMb * 1024 * 1024)
            {
                await RotateLogFileAsync(logFilePath, cancellationToken);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Writes audit log entry to Application Insights
    /// </summary>
    private Task LogToApplicationInsightsAsync(AuditLogEntry logEntry, CancellationToken cancellationToken)
    {
        if (_telemetryClient == null)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            try
            {
                var eventTelemetry = new EventTelemetry($"AgentAudit.{logEntry.EventType}")
                {
                    Timestamp = logEntry.Timestamp
                };

                // Add standard properties
                eventTelemetry.Properties["EventId"] = logEntry.EventId;
                eventTelemetry.Properties["AgentId"] = logEntry.AgentId;
                eventTelemetry.Properties["SessionId"] = logEntry.SessionId;
                eventTelemetry.Properties["Severity"] = logEntry.Severity;

                if (logEntry.UserId != null)
                    eventTelemetry.Properties["UserId"] = logEntry.UserId;

                // Add event data
                eventTelemetry.Properties["Command"] = logEntry.EventData.Command;

                if (logEntry.EventData.Result != null)
                    eventTelemetry.Properties["Result"] = logEntry.EventData.Result;

                if (logEntry.EventData.ErrorMessage != null)
                    eventTelemetry.Properties["ErrorMessage"] = logEntry.EventData.ErrorMessage;

                // Add security context
                if (logEntry.SecurityContext.SourceIpAddress != null)
                    eventTelemetry.Properties["SourceIP"] = logEntry.SecurityContext.SourceIpAddress;

                if (logEntry.SecurityContext.AuthenticationMethod != null)
                    eventTelemetry.Properties["AuthMethod"] = logEntry.SecurityContext.AuthenticationMethod;

                if (logEntry.SecurityContext.AuthorizationDecision != null)
                    eventTelemetry.Properties["AuthDecision"] = logEntry.SecurityContext.AuthorizationDecision;

                // Add resource context
                if (logEntry.ResourceContext != null)
                {
                    if (logEntry.ResourceContext.SubscriptionId != null)
                        eventTelemetry.Properties["SubscriptionId"] = logEntry.ResourceContext.SubscriptionId;

                    if (logEntry.ResourceContext.ResourceGroup != null)
                        eventTelemetry.Properties["ResourceGroup"] = logEntry.ResourceContext.ResourceGroup;

                    if (logEntry.ResourceContext.ResourceId != null)
                        eventTelemetry.Properties["ResourceId"] = logEntry.ResourceContext.ResourceId;
                }

                // Add metrics
                if (logEntry.EventData.ExecutionTimeMs.HasValue)
                {
                    eventTelemetry.Metrics["ExecutionTimeMs"] = logEntry.EventData.ExecutionTimeMs.Value;
                }

                if (logEntry.EventData.Result != null)
                {
                    eventTelemetry.Metrics["OutputSize"] = logEntry.EventData.Result.Length;
                }

                // Add integrity hash
                eventTelemetry.Properties["IntegrityHash"] = logEntry.IntegrityHash;

                _telemetryClient.TrackEvent(eventTelemetry);

                _logger.LogTrace("Audit log sent to Application Insights: {EventId}", logEntry.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send audit log to Application Insights: {Error}", ex.Message);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Gets the log file path for a given date
    /// </summary>
    private string GetLogFilePath(DateTimeOffset timestamp)
    {
        var dateStr = timestamp.ToString("yyyy-MM-dd");
        return Path.Combine(_options.LogDirectory, $"{dateStr}.log.json");
    }

    /// <summary>
    /// Ensures the log directory exists
    /// </summary>
    private void EnsureLogDirectoryExists()
    {
        if (!Directory.Exists(_options.LogDirectory))
        {
            Directory.CreateDirectory(_options.LogDirectory);
            _logger.LogInformation("Created audit log directory: {Directory}", _options.LogDirectory);
        }
    }

    /// <summary>
    /// Rotates a log file when it exceeds maximum size
    /// </summary>
    private async Task RotateLogFileAsync(string logFilePath, CancellationToken cancellationToken)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("HHmmss");
            var directory = Path.GetDirectoryName(logFilePath)!;
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(logFilePath);
            var rotatedPath = Path.Combine(directory, $"{fileNameWithoutExt}.{timestamp}.log.json");

            File.Move(logFilePath, rotatedPath);

            _logger.LogInformation("Rotated log file: {OldPath} -> {NewPath}", logFilePath, rotatedPath);

            if (_options.EnableCompression)
            {
                await CompressLogFileAsync(rotatedPath, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate log file: {FilePath}", logFilePath);
        }
    }

    /// <summary>
    /// Compresses a log file using gzip
    /// </summary>
    private async Task CompressLogFileAsync(string logFilePath, CancellationToken cancellationToken)
    {
        try
        {
            var compressedPath = logFilePath + ".gz";

            await using var sourceStream = File.OpenRead(logFilePath);
            await using var compressedStream = File.Create(compressedPath);
            await using var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal);

            await sourceStream.CopyToAsync(gzipStream, cancellationToken);
            await gzipStream.FlushAsync(cancellationToken);

            File.Delete(logFilePath);

            _logger.LogInformation("Compressed log file: {FilePath}", compressedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compress log file: {FilePath}", logFilePath);
        }
    }

    /// <summary>
    /// Gets log files within a date range
    /// </summary>
    private IEnumerable<string> GetLogFilesInDateRange(DateTimeOffset startDate, DateTimeOffset endDate)
    {
        var logFiles = new List<string>();

        if (!Directory.Exists(_options.LogDirectory))
            return logFiles;

        var allFiles = Directory.GetFiles(_options.LogDirectory, "*.log.json*");

        foreach (var file in allFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);

            // Handle rotated files (e.g., 2025-10-28.120000.log.json)
            var datePart = fileName.Split('.')[0];

            if (DateTimeOffset.TryParse(datePart, out var fileDate))
            {
                if (fileDate.Date >= startDate.Date && fileDate.Date <= endDate.Date)
                {
                    logFiles.Add(file);
                }
            }
        }

        return logFiles.OrderBy(f => f);
    }

    /// <summary>
    /// Reads log entries from a file
    /// </summary>
    private async IAsyncEnumerable<AuditLogEntry> ReadLogEntriesAsync(
        string logFilePath,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Stream fileStream;

        if (logFilePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            var compressedStream = File.OpenRead(logFilePath);
            fileStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        }
        else
        {
            fileStream = File.OpenRead(logFilePath);
        }

        await using (fileStream)
        using (var reader = new StreamReader(fileStream))
        {
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                AuditLogEntry? entry = null;
                try
                {
                    entry = JsonSerializer.Deserialize<AuditLogEntry>(line, _jsonOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize log entry from file: {FilePath}", logFilePath);
                }

                if (entry != null)
                {
                    yield return entry;
                }
            }
        }
    }

    /// <summary>
    /// Starts background cleanup of old log files
    /// </summary>
    private void StartBackgroundCleanup()
    {
        Task.Run(async () =>
        {
            while (!_disposed)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(24));
                    await CleanupOldLogsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during log cleanup: {Error}", ex.Message);
                }
            }
        });
    }

    /// <summary>
    /// Cleans up old log files based on retention policy
    /// </summary>
    private async Task CleanupOldLogsAsync()
    {
        if (!Directory.Exists(_options.LogDirectory))
            return;

        var now = DateTime.UtcNow;
        var allFiles = Directory.GetFiles(_options.LogDirectory, "*.*");

        foreach (var file in allFiles)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                var age = now - fileInfo.CreationTimeUtc;

                var isCompressed = file.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
                var retentionDays = isCompressed
                    ? _options.CompressedLogRetentionDays
                    : _options.LogRetentionDays;

                if (age.TotalDays > retentionDays)
                {
                    File.Delete(file);
                    _logger.LogInformation("Deleted old log file: {FilePath} (Age: {Age} days)", file, (int)age.TotalDays);
                }
                else if (!isCompressed && _options.EnableCompression && age.TotalDays > 1)
                {
                    // Compress files older than 1 day
                    await CompressLogFileAsync(file, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process log file during cleanup: {FilePath}", file);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _fileLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
