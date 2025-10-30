using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WAiSA.API.Security.Staging.Examples
{
    /// <summary>
    /// Example controller demonstrating secure script staging and execution.
    /// </summary>
    [ApiController]
    [Route("api/agent/[controller]")]
    public class ExecutionController : ControllerBase
    {
        private readonly IStagingManager _stagingManager;
        private readonly ILogger<ExecutionController> _logger;

        public ExecutionController(
            IStagingManager stagingManager,
            ILogger<ExecutionController> logger)
        {
            _stagingManager = stagingManager ?? throw new ArgumentNullException(nameof(stagingManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes an AI-generated script in a secure staging environment.
        /// </summary>
        /// <param name="request">The script execution request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Execution result with checksum and output.</returns>
        [HttpPost("execute")]
        [ProducesResponseType(typeof(ScriptExecutionResponse), 200)]
        [ProducesResponseType(typeof(ValidationErrorResponse), 400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> ExecuteScript(
            [FromBody] ScriptExecutionRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(new ValidationErrorResponse
                {
                    Message = "Request body cannot be null"
                });
            }

            StagingEnvironment environment = null;

            try
            {
                _logger.LogInformation(
                    "Received script execution request. Agent: {AgentId}, Session: {SessionId}, Script: {ScriptName}",
                    request.AgentId,
                    request.SessionId,
                    request.ScriptName);

                // Step 1: Create isolated staging environment
                environment = await _stagingManager.CreateStagingEnvironmentAsync(
                    agentId: request.AgentId,
                    sessionId: request.SessionId,
                    cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Staging environment created: {RootPath}, Expires: {ExpiresAt}",
                    environment.RootPath,
                    environment.ExpiresAt);

                // Step 2: Validate and stage the script
                var validationResult = await _stagingManager.ValidateAndStageScriptAsync(
                    environment: environment,
                    content: request.ScriptContent,
                    name: request.ScriptName,
                    cancellationToken: cancellationToken);

                if (!validationResult.IsValid)
                {
                    _logger.LogWarning(
                        "Script validation failed. Errors: {Errors}",
                        string.Join(", ", validationResult.Errors));

                    return BadRequest(new ValidationErrorResponse
                    {
                        Message = "Script validation failed",
                        Errors = validationResult.Errors,
                        Warnings = validationResult.Warnings
                    });
                }

                _logger.LogInformation(
                    "Script validated successfully. Checksum: {Checksum}, Size: {Size} bytes, Warnings: {WarningCount}",
                    validationResult.Checksum,
                    validationResult.FileSizeBytes,
                    validationResult.Warnings.Count);

                // Step 3: Execute the script in sandbox (implementation-specific)
                // This is where you would integrate with your script execution engine
                var executionOutput = await ExecuteScriptInSandboxAsync(
                    scriptPath: validationResult.StagedFilePath,
                    outputPath: environment.OutputsPath,
                    logPath: environment.LogsPath,
                    timeout: request.TimeoutSeconds,
                    cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Script execution completed successfully. Exit Code: {ExitCode}",
                    executionOutput.ExitCode);

                // Step 4: Return results
                return Ok(new ScriptExecutionResponse
                {
                    Success = true,
                    Checksum = validationResult.Checksum,
                    FileSizeBytes = validationResult.FileSizeBytes,
                    Warnings = validationResult.Warnings,
                    ExecutionOutput = executionOutput,
                    StagingEnvironmentPath = environment.RootPath,
                    ExpiresAt = environment.ExpiresAt
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request parameters");
                return BadRequest(new ValidationErrorResponse
                {
                    Message = ex.Message
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Script execution was cancelled");
                return StatusCode(499, new { message = "Request cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Script execution failed with unexpected error");
                return StatusCode(500, new
                {
                    message = "An unexpected error occurred during script execution",
                    error = ex.Message
                });
            }
            finally
            {
                // Auto-cleanup: Environment will be cleaned up via Dispose
                // or by the periodic cleanup timer after expiration
                if (environment != null && request?.CleanupImmediately == true)
                {
                    _logger.LogInformation(
                        "Performing immediate cleanup of staging environment: {RootPath}",
                        environment.RootPath);

                    await _stagingManager.CleanupStagingEnvironmentAsync(
                        environment,
                        CancellationToken.None); // Don't cancel cleanup
                }
                else
                {
                    // Let the environment expire naturally (1 hour)
                    environment?.Dispose();
                }
            }
        }

        /// <summary>
        /// Gets the status of a staging environment.
        /// </summary>
        [HttpGet("environment/{agentId}/{sessionId}")]
        public IActionResult GetEnvironmentStatus(string agentId, string sessionId)
        {
            // Implementation would track active environments
            // This is a simplified example
            return Ok(new
            {
                basePath = _stagingManager.GetBaseStagingPath(),
                platform = Environment.OSVersion.Platform.ToString(),
                message = "Use this endpoint to check environment status"
            });
        }

        /// <summary>
        /// Simulates script execution in a sandbox environment.
        /// Replace this with your actual script execution logic.
        /// </summary>
        private async Task<ExecutionOutput> ExecuteScriptInSandboxAsync(
            string scriptPath,
            string outputPath,
            string logPath,
            int timeout,
            CancellationToken cancellationToken)
        {
            // This is a placeholder implementation
            // In production, you would:
            // 1. Use a sandboxed execution environment (Docker, Firecracker, etc.)
            // 2. Set resource limits (CPU, memory, disk)
            // 3. Capture stdout/stderr
            // 4. Enforce timeout
            // 5. Collect output files

            _logger.LogInformation(
                "Executing script: {ScriptPath} (timeout: {Timeout}s)",
                scriptPath,
                timeout);

            // Simulate execution
            await Task.Delay(100, cancellationToken);

            return new ExecutionOutput
            {
                ExitCode = 0,
                StandardOutput = "Script executed successfully",
                StandardError = string.Empty,
                ExecutionTimeMs = 100,
                OutputFiles = Array.Empty<string>()
            };
        }
    }

    #region Request/Response Models

    /// <summary>
    /// Request model for script execution.
    /// </summary>
    public class ScriptExecutionRequest
    {
        /// <summary>
        /// Gets or sets the agent identifier.
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets the script name.
        /// </summary>
        public string ScriptName { get; set; }

        /// <summary>
        /// Gets or sets the script content.
        /// </summary>
        public string ScriptContent { get; set; }

        /// <summary>
        /// Gets or sets the execution timeout in seconds.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300; // Default 5 minutes

        /// <summary>
        /// Gets or sets a value indicating whether to cleanup immediately after execution.
        /// </summary>
        public bool CleanupImmediately { get; set; } = false;
    }

    /// <summary>
    /// Response model for successful script execution.
    /// </summary>
    public class ScriptExecutionResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether execution was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the SHA256 checksum of the executed script.
        /// </summary>
        public string Checksum { get; set; }

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets validation warnings.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> Warnings { get; set; }

        /// <summary>
        /// Gets or sets the execution output.
        /// </summary>
        public ExecutionOutput ExecutionOutput { get; set; }

        /// <summary>
        /// Gets or sets the staging environment path.
        /// </summary>
        public string StagingEnvironmentPath { get; set; }

        /// <summary>
        /// Gets or sets the expiration timestamp.
        /// </summary>
        public DateTimeOffset ExpiresAt { get; set; }
    }

    /// <summary>
    /// Response model for validation errors.
    /// </summary>
    public class ValidationErrorResponse
    {
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the validation errors.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> Errors { get; set; }

        /// <summary>
        /// Gets or sets the validation warnings.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> Warnings { get; set; }
    }

    /// <summary>
    /// Represents the output of script execution.
    /// </summary>
    public class ExecutionOutput
    {
        /// <summary>
        /// Gets or sets the exit code.
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Gets or sets the standard output.
        /// </summary>
        public string StandardOutput { get; set; }

        /// <summary>
        /// Gets or sets the standard error.
        /// </summary>
        public string StandardError { get; set; }

        /// <summary>
        /// Gets or sets the execution time in milliseconds.
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the output files generated.
        /// </summary>
        public string[] OutputFiles { get; set; }
    }

    #endregion
}
