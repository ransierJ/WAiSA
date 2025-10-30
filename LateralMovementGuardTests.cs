// UNIT TESTS FOR LATERAL MOVEMENT GUARD
// =====================================
// Move this file to: /home/sysadmin/sysadmin_in_a_box/backend/WAiSA.Tests/Security/Guards/LateralMovementGuardTests.cs
//
// Required NuGet packages in test project:
// - xunit (>= 2.4.2)
// - xunit.runner.visualstudio (>= 2.4.5)
// - Moq (>= 4.18.4)
// - Microsoft.NET.Test.Sdk (>= 17.6.0)

using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using WAiSA.API.Security.Guards;

namespace WAiSA.Tests.Security.Guards;

/// <summary>
/// Unit tests for LateralMovementGuard class.
/// Tests all security validations, edge cases, and thread safety.
/// </summary>
public sealed class LateralMovementGuardTests : IDisposable
{
    private readonly Mock<ILogger<LateralMovementGuard>> _mockLogger;
    private readonly string _testConfigPath;

    public LateralMovementGuardTests()
    {
        _mockLogger = new Mock<ILogger<LateralMovementGuard>>();
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"test-config-{Guid.NewGuid()}.yml");
        CreateTestConfiguration();
    }

    public void Dispose()
    {
        if (File.Exists(_testConfigPath))
        {
            File.Delete(_testConfigPath);
        }
    }

    #region Remote Cmdlet Tests

    [Fact]
    public async Task ValidateCommandAsync_WithEnterPSSession_ShouldBlock()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "Enter-PSSession -ComputerName RemoteServer";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(ViolationType.RemoteCmdlet, result.ViolationType);
        Assert.Contains("Enter-PSSession", result.BlockedReason);
        Assert.True(result.ShouldQuarantine);
    }

    [Fact]
    public async Task ValidateCommandAsync_WithInvokeCommand_ShouldBlock()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "Invoke-Command -ComputerName Server01 -ScriptBlock { Get-Process }";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(ViolationType.RemoteCmdlet, result.ViolationType);
        Assert.Contains("Invoke-Command", result.BlockedReason);
    }

    [Theory]
    [InlineData("New-PSSession")]
    [InlineData("Connect-PSSession")]
    [InlineData("Remove-PSSession")]
    [InlineData("Get-PSSession")]
    [InlineData("New-CimSession")]
    [InlineData("Invoke-WmiMethod")]
    [InlineData("Invoke-CimMethod")]
    public async Task ValidateCommandAsync_WithVariousBlockedCmdlets_ShouldBlock(string cmdlet)
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = $"{cmdlet} -ComputerName Remote";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(ViolationType.RemoteCmdlet, result.ViolationType);
    }

    [Fact]
    public async Task ValidateCommandAsync_CaseInsensitiveCmdletMatching_ShouldBlock()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "ENTER-PSSESSION -ComputerName Remote";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
    }

    #endregion

    #region Remote Parameter Tests

    [Fact]
    public async Task ValidateCommandAsync_WithRemoteComputerName_ShouldBlock()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "Get-Service -ComputerName RemoteServer";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(ViolationType.RemoteParameter, result.ViolationType);
        Assert.Contains("RemoteServer", result.BlockedReason);
    }

    [Fact]
    public async Task ValidateCommandAsync_WithLocalhostComputerName_ShouldAllow()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "Get-Service -ComputerName localhost";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Null(result.ViolationType);
    }

    [Fact]
    public async Task ValidateCommandAsync_With127001ComputerName_ShouldAllow()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "Get-Service -ComputerName 127.0.0.1";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateCommandAsync_WithDotComputerName_ShouldAllow()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "Get-Service -ComputerName .";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.True(result.IsAllowed);
    }

    #endregion

    #region Protocol Tests

    [Fact]
    public async Task ValidateCommandAsync_WithWinRs_ShouldBlock()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "winrs -r:RemoteServer ipconfig";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(ViolationType.RemoteProtocol, result.ViolationType);
        Assert.Contains("WinRM", result.BlockedReason);
    }

    [Fact]
    public async Task ValidateCommandAsync_WithSsh_ShouldBlock()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "ssh user@remotehost 'ls -la'";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(ViolationType.RemoteProtocol, result.ViolationType);
        Assert.Contains("SSH", result.BlockedReason);
    }

    #endregion

    #region Network Restriction Tests

    [Fact]
    public async Task ValidateCommandAsync_WithInternalIp10_ShouldBlock()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "Test-Connection 10.0.0.5";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(ViolationType.NetworkRestriction, result.ViolationType);
        Assert.Contains("10.0.0.5", result.BlockedReason);
    }

    [Fact]
    public async Task ValidateCommandAsync_WithInternalIp192168_ShouldBlock()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "Invoke-WebRequest http://192.168.1.100/api";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(ViolationType.NetworkRestriction, result.ViolationType);
        Assert.Contains("192.168.1.100", result.BlockedReason);
    }

    [Theory]
    [InlineData("172.16.0.1")]
    [InlineData("172.20.5.10")]
    [InlineData("172.31.255.254")]
    public async Task ValidateCommandAsync_WithInternalIp172Range_ShouldBlock(string ip)
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = $"ping {ip}";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(ViolationType.NetworkRestriction, result.ViolationType);
        Assert.Contains(ip, result.BlockedReason);
    }

    #endregion

    #region Safe Command Tests

    [Theory]
    [InlineData("Get-Process")]
    [InlineData("Get-Service")]
    [InlineData("Get-EventLog")]
    [InlineData("Get-Disk")]
    [InlineData("Test-Path")]
    [InlineData("Get-ChildItem")]
    [InlineData("Measure-Command { Get-Process }")]
    public async Task ValidateCommandAsync_WithSafeReadOnlyCmdlets_ShouldAllow(string command)
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateCommandAsync_WithSafeCommand_ShouldAllow()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "Get-Process | Where-Object { $_.CPU -gt 100 }";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Null(result.ViolationType);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task ValidateCommandAsync_WithNullCommand_ShouldAllow()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);

        // Act
        var result = await guard.ValidateCommandAsync(null);

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateCommandAsync_WithEmptyCommand_ShouldAllow()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);

        // Act
        var result = await guard.ValidateCommandAsync(string.Empty);

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateCommandAsync_WithWhitespaceCommand_ShouldAllow()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);

        // Act
        var result = await guard.ValidateCommandAsync("   ");

        // Assert
        Assert.True(result.IsAllowed);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ValidateCommandAsync_ConcurrentCalls_ShouldHandleCorrectly()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var commands = new[]
        {
            "Get-Process",
            "Enter-PSSession -ComputerName Remote",
            "Get-Service -ComputerName localhost",
            "ssh user@host",
            "Test-Connection 10.0.0.1"
        };

        // Act
        var tasks = commands.Select(cmd => guard.ValidateCommandAsync(cmd)).ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5, results.Length);
        Assert.True(results[0].IsAllowed); // Get-Process
        Assert.False(results[1].IsAllowed); // Enter-PSSession
        Assert.True(results[2].IsAllowed); // localhost
        Assert.False(results[3].IsAllowed); // ssh
        Assert.False(results[4].IsAllowed); // 10.0.0.1
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task ReloadConfigurationAsync_ShouldUpdateConfig()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "Enter-PSSession -ComputerName Remote";

        // Act - should block initially
        var result1 = await guard.ValidateCommandAsync(command);

        // Modify config to disable
        CreateTestConfiguration(enabled: false);

        // Reload
        await guard.ReloadConfigurationAsync();

        // Act - should allow after reload
        var result2 = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.False(result1.IsAllowed);
        Assert.True(result2.IsAllowed);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LateralMovementGuard(null, _testConfigPath));
    }

    [Fact]
    public void Constructor_WithEmptyConfigPath_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new LateralMovementGuard(_mockLogger.Object, string.Empty));
    }

    [Fact]
    public void Constructor_WithNonExistentConfigPath_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            new LateralMovementGuard(_mockLogger.Object, "/nonexistent/path.yml"));
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ValidateCommandAsync_WithCancellation_ShouldThrowOperationCancelledException()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await guard.ValidateCommandAsync("Get-Process", cts.Token));
    }

    #endregion

    #region Context Validation Tests

    [Fact]
    public async Task ValidateCommandAsync_WhenBlocked_ShouldIncludeContext()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "Enter-PSSession -ComputerName RemoteServer";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.NotNull(result.Context);
        Assert.True(result.Context.ContainsKey("BlockedCmdlet"));
        Assert.Equal("Enter-PSSession", result.Context["BlockedCmdlet"]);
    }

    [Fact]
    public async Task ValidateCommandAsync_WithRemoteParameter_ShouldIncludeAllowedTargets()
    {
        // Arrange
        var guard = new LateralMovementGuard(_mockLogger.Object, _testConfigPath);
        var command = "Get-Service -ComputerName RemoteHost";

        // Act
        var result = await guard.ValidateCommandAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.True(result.Context.ContainsKey("AllowedTargets"));
    }

    #endregion

    #region Helper Methods

    private void CreateTestConfiguration(bool enabled = true)
    {
        var yaml = $@"agent_security:
  lateral_movement:
    enabled: {enabled.ToString().ToLower()}
    description: ""Test configuration""
    block_remote_execution: true
    blocked_cmdlets:
      - ""Enter-PSSession""
      - ""Invoke-Command""
      - ""New-PSSession""
      - ""Connect-PSSession""
      - ""Remove-PSSession""
      - ""Get-PSSession""
      - ""New-CimSession""
      - ""Invoke-WmiMethod""
      - ""Invoke-CimMethod""
      - ""winrs""
      - ""ssh""
    allowed_targets:
      - ""localhost""
      - ""127.0.0.1""
      - "".""
      - ""$env:COMPUTERNAME""
    network_restrictions:
      deny_outbound_to_internal_networks: true
      allowed_azure_services:
        - ""*.azure.com""
        - ""*.windows.net""
        - ""*.microsoft.com""
      blocked_ports:
        - 22
        - 3389
        - 5985
        - 5986
    on_violation:
      action: ""block""
      notify_security_team: true
      quarantine_agent: true
";
        File.WriteAllText(_testConfigPath, yaml);
    }

    #endregion
}
