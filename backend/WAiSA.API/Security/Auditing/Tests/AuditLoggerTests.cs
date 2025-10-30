using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace WAiSA.API.Security.Auditing.Tests;

/// <summary>
/// Unit tests for AuditLogger
/// </summary>
public class AuditLoggerTests : IDisposable
{
    private readonly string _testLogDirectory;
    private readonly Mock<ILogger<AuditLogger>> _mockLogger;
    private readonly AuditLoggerOptions _options;
    private readonly AuditLogger _auditLogger;

    public AuditLoggerTests()
    {
        _testLogDirectory = Path.Combine(Path.GetTempPath(), $"audit-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testLogDirectory);

        _mockLogger = new Mock<ILogger<AuditLogger>>();

        _options = new AuditLoggerOptions
        {
            EnableFileLogging = true,
            LogDirectory = _testLogDirectory,
            EnableApplicationInsights = false,
            EnableCompression = false, // Disable for easier testing
            LogRetentionDays = 1,
            IncludeStackTraces = true
        };

        var optionsWrapper = Options.Create(_options);
        _auditLogger = new AuditLogger(_mockLogger.Object, optionsWrapper);
    }

    [Fact]
    public async Task LogAgentActionAsync_CreatesLogFile()
    {
        // Arrange
        var actionEvent = CreateTestActionEvent();

        // Act
        await _auditLogger.LogAgentActionAsync(actionEvent);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*.log.json");
        Assert.Single(logFiles);

        var logContent = await File.ReadAllTextAsync(logFiles[0]);
        Assert.NotEmpty(logContent);
    }

    [Fact]
    public async Task LogAgentActionAsync_SanitizesPasswords()
    {
        // Arrange
        var actionEvent = CreateTestActionEvent() with
        {
            Parameters = new Dictionary<string, object>
            {
                ["username"] = "testuser",
                ["password"] = "SuperSecret123!",
                ["apiKey"] = "sk-1234567890",
                ["normalParam"] = "visible"
            }
        };

        // Act
        await _auditLogger.LogAgentActionAsync(actionEvent);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*.log.json");
        var logContent = await File.ReadAllTextAsync(logFiles[0]);
        var logEntry = JsonSerializer.Deserialize<AuditLogEntry>(logContent);

        Assert.NotNull(logEntry);
        Assert.Equal("***REDACTED***", logEntry.EventData.Parameters!["password"]);
        Assert.Equal("***REDACTED***", logEntry.EventData.Parameters["apiKey"]);
        Assert.Equal("visible", logEntry.EventData.Parameters["normalParam"]);
    }

    [Fact]
    public async Task LogAgentActionAsync_CalculatesIntegrityHash()
    {
        // Arrange
        var actionEvent = CreateTestActionEvent();

        // Act
        await _auditLogger.LogAgentActionAsync(actionEvent);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*.log.json");
        var logContent = await File.ReadAllTextAsync(logFiles[0]);
        var logEntry = JsonSerializer.Deserialize<AuditLogEntry>(logContent);

        Assert.NotNull(logEntry);
        Assert.NotNull(logEntry.IntegrityHash);
        Assert.NotEmpty(logEntry.IntegrityHash);
        Assert.Matches("^[a-f0-9]{64}$", logEntry.IntegrityHash); // SHA256 hex format
    }

    [Fact]
    public async Task VerifyIntegrity_ValidHash_ReturnsTrue()
    {
        // Arrange
        var actionEvent = CreateTestActionEvent();
        await _auditLogger.LogAgentActionAsync(actionEvent);

        var logFiles = Directory.GetFiles(_testLogDirectory, "*.log.json");
        var logContent = await File.ReadAllTextAsync(logFiles[0]);
        var logEntry = JsonSerializer.Deserialize<AuditLogEntry>(logContent);

        // Act
        var isValid = _auditLogger.VerifyIntegrity(logEntry!);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task VerifyIntegrity_TamperedData_ReturnsFalse()
    {
        // Arrange
        var actionEvent = CreateTestActionEvent();
        await _auditLogger.LogAgentActionAsync(actionEvent);

        var logFiles = Directory.GetFiles(_testLogDirectory, "*.log.json");
        var logContent = await File.ReadAllTextAsync(logFiles[0]);
        var logEntry = JsonSerializer.Deserialize<AuditLogEntry>(logContent);

        // Tamper with the log entry
        var tamperedEntry = logEntry! with
        {
            EventData = logEntry.EventData with
            {
                Result = "Tampered result"
            }
        };

        // Act
        var isValid = _auditLogger.VerifyIntegrity(tamperedEntry);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task QueryLogsAsync_FiltersByDateRange()
    {
        // Arrange
        var today = DateTimeOffset.UtcNow;
        var yesterday = today.AddDays(-1);
        var tomorrow = today.AddDays(1);

        await _auditLogger.LogAgentActionAsync(CreateTestActionEvent());

        // Act
        var logsInRange = await _auditLogger.QueryLogsAsync(yesterday, tomorrow);
        var logsOutOfRange = await _auditLogger.QueryLogsAsync(
            tomorrow.AddDays(1),
            tomorrow.AddDays(2));

        // Assert
        Assert.NotEmpty(logsInRange);
        Assert.Empty(logsOutOfRange);
    }

    [Fact]
    public async Task QueryLogsAsync_FiltersByAgentId()
    {
        // Arrange
        await _auditLogger.LogAgentActionAsync(CreateTestActionEvent() with
        {
            AgentId = "agent-001"
        });

        await _auditLogger.LogAgentActionAsync(CreateTestActionEvent() with
        {
            AgentId = "agent-002"
        });

        // Act
        var agent001Logs = await _auditLogger.QueryLogsAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            agentId: "agent-001");

        var agent002Logs = await _auditLogger.QueryLogsAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            agentId: "agent-002");

        // Assert
        Assert.Single(agent001Logs);
        Assert.Single(agent002Logs);
        Assert.Equal("agent-001", agent001Logs.First().AgentId);
        Assert.Equal("agent-002", agent002Logs.First().AgentId);
    }

    [Fact]
    public async Task QueryLogsAsync_FiltersByEventType()
    {
        // Arrange
        await _auditLogger.LogAgentActionAsync(CreateTestActionEvent() with
        {
            EventType = EventType.CommandExecution
        });

        await _auditLogger.LogAgentActionAsync(CreateTestActionEvent() with
        {
            EventType = EventType.SecurityViolation
        });

        // Act
        var commandLogs = await _auditLogger.QueryLogsAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            eventType: EventType.CommandExecution);

        var securityLogs = await _auditLogger.QueryLogsAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            eventType: EventType.SecurityViolation);

        // Assert
        Assert.Single(commandLogs);
        Assert.Single(securityLogs);
        Assert.Equal("CommandExecution", commandLogs.First().EventType);
        Assert.Equal("SecurityViolation", securityLogs.First().EventType);
    }

    [Fact]
    public async Task LogAgentActionAsync_HandlesNestedParameters()
    {
        // Arrange
        var actionEvent = CreateTestActionEvent() with
        {
            Parameters = new Dictionary<string, object>
            {
                ["outer"] = "value",
                ["nested"] = new Dictionary<string, object>
                {
                    ["inner"] = "value",
                    ["secret"] = "should-be-redacted"
                }
            }
        };

        // Act
        await _auditLogger.LogAgentActionAsync(actionEvent);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*.log.json");
        var logContent = await File.ReadAllTextAsync(logFiles[0]);
        var logEntry = JsonSerializer.Deserialize<AuditLogEntry>(logContent);

        Assert.NotNull(logEntry);
        var nestedDict = logEntry.EventData.Parameters!["nested"] as JsonElement?;
        Assert.NotNull(nestedDict);

        var nestedObj = JsonSerializer.Deserialize<Dictionary<string, object>>(
            nestedDict.Value.GetRawText());

        Assert.Equal("***REDACTED***", nestedObj!["secret"].ToString());
    }

    [Fact]
    public async Task LogAgentActionAsync_ThreadSafe_ConcurrentWrites()
    {
        // Arrange
        var tasks = new List<Task>();
        const int concurrentWrites = 100;

        // Act
        for (int i = 0; i < concurrentWrites; i++)
        {
            var index = i;
            tasks.Add(_auditLogger.LogAgentActionAsync(CreateTestActionEvent() with
            {
                Command = $"Command-{index}"
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*.log.json");
        var logContent = await File.ReadAllTextAsync(logFiles[0]);
        var lines = logContent.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(concurrentWrites, lines.Length);
    }

    [Fact]
    public async Task LogAgentActionAsync_IncludesAllRequiredFields()
    {
        // Arrange
        var actionEvent = new AgentActionEvent
        {
            AgentId = "test-agent",
            SessionId = "test-session",
            UserId = "test-user",
            EventType = EventType.CommandExecution,
            Severity = Severity.Info,
            Command = "TestCommand",
            Parameters = new Dictionary<string, object> { ["key"] = "value" },
            Result = "success",
            ExecutionTimeMs = 123,
            SourceIpAddress = "192.168.1.1",
            AuthenticationMethod = "OAuth2",
            AuthorizationDecision = "Allowed",
            SubscriptionId = "sub-123",
            ResourceGroup = "rg-test",
            ResourceId = "/subscriptions/sub-123/resourceGroups/rg-test"
        };

        // Act
        await _auditLogger.LogAgentActionAsync(actionEvent);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*.log.json");
        var logContent = await File.ReadAllTextAsync(logFiles[0]);
        var logEntry = JsonSerializer.Deserialize<AuditLogEntry>(logContent);

        Assert.NotNull(logEntry);
        Assert.Equal("test-agent", logEntry.AgentId);
        Assert.Equal("test-session", logEntry.SessionId);
        Assert.Equal("test-user", logEntry.UserId);
        Assert.Equal("CommandExecution", logEntry.EventType);
        Assert.Equal("Info", logEntry.Severity);
        Assert.Equal("TestCommand", logEntry.EventData.Command);
        Assert.Equal("success", logEntry.EventData.Result);
        Assert.Equal(123, logEntry.EventData.ExecutionTimeMs);
        Assert.Equal("192.168.1.1", logEntry.SecurityContext.SourceIpAddress);
        Assert.Equal("OAuth2", logEntry.SecurityContext.AuthenticationMethod);
        Assert.Equal("Allowed", logEntry.SecurityContext.AuthorizationDecision);
        Assert.NotNull(logEntry.ResourceContext);
        Assert.Equal("sub-123", logEntry.ResourceContext.SubscriptionId);
        Assert.Equal("rg-test", logEntry.ResourceContext.ResourceGroup);
    }

    [Theory]
    [InlineData("password", "***REDACTED***")]
    [InlineData("secret", "***REDACTED***")]
    [InlineData("apikey", "***REDACTED***")]
    [InlineData("api_key", "***REDACTED***")]
    [InlineData("token", "***REDACTED***")]
    [InlineData("credential", "***REDACTED***")]
    [InlineData("connectionstring", "***REDACTED***")]
    [InlineData("normalKey", "normalValue")]
    public async Task SanitizeParameters_RedactsSensitiveKeys(string key, string expectedValue)
    {
        // Arrange
        var actualValue = key == "normalKey" ? "normalValue" : "secretValue";
        var actionEvent = CreateTestActionEvent() with
        {
            Parameters = new Dictionary<string, object>
            {
                [key] = actualValue
            }
        };

        // Act
        await _auditLogger.LogAgentActionAsync(actionEvent);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*.log.json");
        var logContent = await File.ReadAllTextAsync(logFiles[0]);
        var logEntry = JsonSerializer.Deserialize<AuditLogEntry>(logContent);

        Assert.NotNull(logEntry);
        Assert.Equal(expectedValue, logEntry.EventData.Parameters![key].ToString());
    }

    private AgentActionEvent CreateTestActionEvent()
    {
        return new AgentActionEvent
        {
            AgentId = "test-agent-" + Guid.NewGuid().ToString("N")[..8],
            SessionId = "session-" + Guid.NewGuid().ToString("N")[..8],
            UserId = "user-test",
            EventType = EventType.CommandExecution,
            Severity = Severity.Info,
            Command = "TestCommand",
            Parameters = new Dictionary<string, object>
            {
                ["param1"] = "value1",
                ["param2"] = 42
            },
            Result = "Operation completed successfully",
            ExecutionTimeMs = 100,
            SourceIpAddress = "127.0.0.1",
            AuthenticationMethod = "Test",
            AuthorizationDecision = "Allowed"
        };
    }

    public void Dispose()
    {
        _auditLogger?.Dispose();

        if (Directory.Exists(_testLogDirectory))
        {
            try
            {
                Directory.Delete(_testLogDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
