namespace WAiSA.API.Security.Auditing;

/// <summary>
/// Interface for audit logging of AI agent actions
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Logs an agent action event with comprehensive audit information
    /// </summary>
    /// <param name="actionEvent">The agent action event to log</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task LogAgentActionAsync(AgentActionEvent actionEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries audit logs based on filter criteria
    /// </summary>
    /// <param name="startDate">Start date for query</param>
    /// <param name="endDate">End date for query</param>
    /// <param name="agentId">Optional agent ID filter</param>
    /// <param name="userId">Optional user ID filter</param>
    /// <param name="eventType">Optional event type filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of matching audit log entries</returns>
    Task<IEnumerable<AuditLogEntry>> QueryLogsAsync(
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string? agentId = null,
        string? userId = null,
        EventType? eventType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the integrity of an audit log entry
    /// </summary>
    /// <param name="logEntry">The log entry to verify</param>
    /// <returns>True if integrity hash is valid, false otherwise</returns>
    bool VerifyIntegrity(AuditLogEntry logEntry);
}
