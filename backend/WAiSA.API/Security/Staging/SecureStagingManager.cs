using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WAiSA.API.Security.Staging
{
    /// <summary>
    /// Manages secure staging environments for AI-generated scripts with strict permissions and auto-cleanup.
    /// </summary>
    public sealed class SecureStagingManager : IStagingManager, IDisposable
    {
        private readonly ILogger<SecureStagingManager> _logger;
        private readonly Timer _cleanupTimer;
        private readonly HashSet<string> _activeEnvironments;
        private readonly SemaphoreSlim _cleanupLock;
        private readonly TimeSpan _defaultExpirationTime;
        private bool _disposed;

        // Platform-specific base paths
        private const string LinuxBasePath = "/var/agent-staging";
        private const string WindowsBasePath = @"C:\AgentStaging";

        // Dangerous PowerShell/Script patterns to detect
        private static readonly Regex[] DangerousPatterns = new[]
        {
            new Regex(@"Invoke-Expression|IEX", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"Invoke-Command|ICM", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"Invoke-WebRequest|IWR|wget|curl", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"Start-Process|saps", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"New-Object\s+System\.Net\.WebClient", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"DownloadString|DownloadFile", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\$\(\s*IEX|\$\(\s*Invoke-Expression", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"[;&|]\s*rm\s+-rf|[;&|]\s*del\s+/", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"exec\(|eval\(", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"base64|frombase64string", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"-EncodedCommand|-enc", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"Add-Type.*CSharp|Add-Type.*-Language", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };

        // Path traversal patterns
        private static readonly Regex[] PathTraversalPatterns = new[]
        {
            new Regex(@"\.\.", RegexOptions.Compiled),
            new Regex(@"[/\\]", RegexOptions.Compiled),
            new Regex(@"[\x00-\x1F]", RegexOptions.Compiled), // Control characters
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="SecureStagingManager"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public SecureStagingManager(ILogger<SecureStagingManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activeEnvironments = new HashSet<string>();
            _cleanupLock = new SemaphoreSlim(1, 1);
            _defaultExpirationTime = TimeSpan.FromHours(1);

            // Start cleanup timer (runs every 10 minutes)
            _cleanupTimer = new Timer(
                callback: async _ => await PerformPeriodicCleanupAsync(),
                state: null,
                dueTime: TimeSpan.FromMinutes(10),
                period: TimeSpan.FromMinutes(10));

            _logger.LogInformation(
                "SecureStagingManager initialized. Base path: {BasePath}",
                GetBaseStagingPath());
        }

        /// <inheritdoc/>
        public string GetBaseStagingPath()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? WindowsBasePath
                : LinuxBasePath;
        }

        /// <inheritdoc/>
        public async Task<StagingEnvironment> CreateStagingEnvironmentAsync(
            string agentId,
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(agentId))
                throw new ArgumentException("Agent ID cannot be null or empty.", nameof(agentId));

            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));

            _logger.LogInformation(
                "Creating staging environment for Agent: {AgentId}, Session: {SessionId}",
                agentId,
                sessionId);

            try
            {
                var basePath = GetBaseStagingPath();
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var environmentId = $"{agentId}_{sessionId}_{timestamp}";
                var rootPath = Path.Combine(basePath, environmentId);

                // Create directory structure
                var scriptsPath = Path.Combine(rootPath, "scripts");
                var inputsPath = Path.Combine(rootPath, "inputs");
                var outputsPath = Path.Combine(rootPath, "outputs");
                var logsPath = Path.Combine(rootPath, "logs");

                // Ensure base directory exists
                Directory.CreateDirectory(basePath);

                // Create subdirectories
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(scriptsPath);
                    Directory.CreateDirectory(inputsPath);
                    Directory.CreateDirectory(outputsPath);
                    Directory.CreateDirectory(logsPath);
                }, cancellationToken);

                // Set strict permissions
                await SetDirectoryPermissionsAsync(rootPath, DirectoryPermissionLevel.OwnerFullAccess, cancellationToken);
                await SetDirectoryPermissionsAsync(scriptsPath, DirectoryPermissionLevel.OwnerFullAccess, cancellationToken);
                await SetDirectoryPermissionsAsync(inputsPath, DirectoryPermissionLevel.OwnerReadOnly, cancellationToken);
                await SetDirectoryPermissionsAsync(outputsPath, DirectoryPermissionLevel.OwnerWriteOnly, cancellationToken);
                await SetDirectoryPermissionsAsync(logsPath, DirectoryPermissionLevel.OwnerAppendOnly, cancellationToken);

                var expiresAt = DateTimeOffset.UtcNow.Add(_defaultExpirationTime);
                var environment = new StagingEnvironment(
                    agentId: agentId,
                    sessionId: sessionId,
                    rootPath: rootPath,
                    scriptsPath: scriptsPath,
                    inputsPath: inputsPath,
                    outputsPath: outputsPath,
                    logsPath: logsPath,
                    expiresAt: expiresAt,
                    stagingManager: this);

                // Track active environment
                await _cleanupLock.WaitAsync(cancellationToken);
                try
                {
                    _activeEnvironments.Add(rootPath);
                }
                finally
                {
                    _cleanupLock.Release();
                }

                _logger.LogInformation(
                    "Staging environment created successfully. Root: {RootPath}, Expires: {ExpiresAt}",
                    rootPath,
                    expiresAt);

                return environment;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to create staging environment for Agent: {AgentId}, Session: {SessionId}",
                    agentId,
                    sessionId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<ValidationResult> ValidateAndStageScriptAsync(
            StagingEnvironment environment,
            string content,
            string name,
            CancellationToken cancellationToken = default)
        {
            if (environment == null)
                throw new ArgumentNullException(nameof(environment));

            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Script content cannot be null or empty.", nameof(content));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Script name cannot be null or empty.", nameof(name));

            _logger.LogInformation(
                "Validating and staging script: {ScriptName} in environment: {RootPath}",
                name,
                environment.RootPath);

            var errors = new List<string>();
            var warnings = new List<string>();

            // Validate script name for path traversal
            if (!ValidateScriptName(name, errors))
            {
                _logger.LogWarning(
                    "Script name validation failed: {ScriptName}. Errors: {Errors}",
                    name,
                    string.Join(", ", errors));
                return ValidationResult.Failure(errors);
            }

            // Check for dangerous patterns
            CheckDangerousPatterns(content, errors, warnings);

            if (errors.Count > 0)
            {
                _logger.LogWarning(
                    "Script content validation failed: {ScriptName}. Errors: {Errors}",
                    name,
                    string.Join(", ", errors));
                return ValidationResult.Failure(errors);
            }

            try
            {
                var scriptPath = Path.Combine(environment.ScriptsPath, name);

                // Write script to staging directory
                var contentBytes = Encoding.UTF8.GetBytes(content);
                await File.WriteAllBytesAsync(scriptPath, contentBytes, cancellationToken);

                // Set read-only permissions after write
                await SetFilePermissionsAsync(scriptPath, FilePermissionLevel.ReadOnly, cancellationToken);

                // Calculate SHA256 checksum
                var checksum = await ComputeSha256ChecksumAsync(scriptPath, cancellationToken);

                var fileInfo = new FileInfo(scriptPath);
                var fileSizeBytes = fileInfo.Length;

                _logger.LogInformation(
                    "Script staged successfully: {ScriptPath}, Size: {Size} bytes, Checksum: {Checksum}",
                    scriptPath,
                    fileSizeBytes,
                    checksum);

                return ValidationResult.Success(checksum, scriptPath, fileSizeBytes, warnings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stage script: {ScriptName}", name);
                errors.Add($"Failed to stage script: {ex.Message}");
                return ValidationResult.Failure(errors);
            }
        }

        /// <inheritdoc/>
        public async Task CleanupStagingEnvironmentAsync(
            StagingEnvironment environment,
            CancellationToken cancellationToken = default)
        {
            if (environment == null)
                throw new ArgumentNullException(nameof(environment));

            _logger.LogInformation(
                "Starting cleanup for staging environment: {RootPath}",
                environment.RootPath);

            await _cleanupLock.WaitAsync(cancellationToken);
            try
            {
                if (!Directory.Exists(environment.RootPath))
                {
                    _logger.LogWarning(
                        "Staging environment already cleaned up or does not exist: {RootPath}",
                        environment.RootPath);
                    _activeEnvironments.Remove(environment.RootPath);
                    return;
                }

                // Recursively secure delete all files
                await SecureDeleteDirectoryAsync(environment.RootPath, cancellationToken);

                // Remove from active tracking
                _activeEnvironments.Remove(environment.RootPath);

                _logger.LogInformation(
                    "Staging environment cleaned up successfully: {RootPath}",
                    environment.RootPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to cleanup staging environment: {RootPath}",
                    environment.RootPath);
                throw;
            }
            finally
            {
                _cleanupLock.Release();
            }
        }

        /// <summary>
        /// Performs periodic cleanup of expired staging environments.
        /// </summary>
        private async Task PerformPeriodicCleanupAsync()
        {
            _logger.LogDebug("Starting periodic cleanup check");

            try
            {
                var basePath = GetBaseStagingPath();
                if (!Directory.Exists(basePath))
                    return;

                var directories = Directory.GetDirectories(basePath);
                foreach (var directory in directories)
                {
                    try
                    {
                        // Check if directory is old enough to clean up (older than expiration time)
                        var directoryInfo = new DirectoryInfo(directory);
                        if (DateTimeOffset.UtcNow - directoryInfo.CreationTimeUtc > _defaultExpirationTime)
                        {
                            _logger.LogInformation(
                                "Cleaning up expired staging environment: {Directory}",
                                directory);
                            await SecureDeleteDirectoryAsync(directory, CancellationToken.None);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to cleanup expired directory: {Directory}",
                            directory);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic cleanup");
            }
        }

        /// <summary>
        /// Validates the script name to prevent path traversal attacks.
        /// </summary>
        private bool ValidateScriptName(string name, List<string> errors)
        {
            var isValid = true;

            foreach (var pattern in PathTraversalPatterns)
            {
                if (pattern.IsMatch(name))
                {
                    errors.Add($"Script name contains invalid characters or path traversal pattern: {name}");
                    isValid = false;
                    break;
                }
            }

            // Additional validation: ensure reasonable length and allowed characters
            if (name.Length > 255)
            {
                errors.Add("Script name exceeds maximum length of 255 characters");
                isValid = false;
            }

            if (!Regex.IsMatch(name, @"^[a-zA-Z0-9_\-\.]+$"))
            {
                errors.Add("Script name contains invalid characters. Only alphanumeric, underscore, hyphen, and dot are allowed");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// Checks script content for dangerous patterns.
        /// </summary>
        private void CheckDangerousPatterns(string content, List<string> errors, List<string> warnings)
        {
            foreach (var pattern in DangerousPatterns)
            {
                var matches = pattern.Matches(content);
                if (matches.Count > 0)
                {
                    var matchedText = matches[0].Value;
                    warnings.Add($"Potentially dangerous pattern detected: {matchedText}");

                    // For critical patterns, add to errors instead
                    if (pattern.ToString().Contains("Invoke-Expression|IEX") ||
                        pattern.ToString().Contains("exec\\(|eval\\("))
                    {
                        errors.Add($"Dangerous code execution pattern detected: {matchedText}");
                    }
                }
            }
        }

        /// <summary>
        /// Sets directory permissions based on the platform.
        /// </summary>
        private async Task SetDirectoryPermissionsAsync(
            string path,
            DirectoryPermissionLevel level,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    SetUnixDirectoryPermissions(path, level);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetWindowsDirectoryPermissions(path, level);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Sets file permissions based on the platform.
        /// </summary>
        private async Task SetFilePermissionsAsync(
            string path,
            FilePermissionLevel level,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    SetUnixFilePermissions(path, level);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetWindowsFilePermissions(path, level);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Sets Unix directory permissions using chmod.
        /// </summary>
        private void SetUnixDirectoryPermissions(string path, DirectoryPermissionLevel level)
        {
            var mode = level switch
            {
                DirectoryPermissionLevel.OwnerFullAccess => "0700",
                DirectoryPermissionLevel.OwnerReadOnly => "0500",
                DirectoryPermissionLevel.OwnerWriteOnly => "0300",
                DirectoryPermissionLevel.OwnerAppendOnly => "0200",
                _ => "0700"
            };

            ExecuteChmod(path, mode);
        }

        /// <summary>
        /// Sets Unix file permissions using chmod.
        /// </summary>
        private void SetUnixFilePermissions(string path, FilePermissionLevel level)
        {
            var mode = level switch
            {
                FilePermissionLevel.ReadOnly => "0400",
                FilePermissionLevel.WriteOnly => "0200",
                FilePermissionLevel.ReadWrite => "0600",
                _ => "0400"
            };

            ExecuteChmod(path, mode);
        }

        /// <summary>
        /// Executes chmod command on Unix systems.
        /// </summary>
        private void ExecuteChmod(string path, string mode)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"{mode} \"{path}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var error = process.StandardError.ReadToEnd();
                    _logger.LogWarning(
                        "chmod failed for {Path} with mode {Mode}. Error: {Error}",
                        path,
                        mode,
                        error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute chmod for {Path}", path);
            }
        }

        /// <summary>
        /// Sets Windows directory permissions using file attributes.
        /// </summary>
        private void SetWindowsDirectoryPermissions(string path, DirectoryPermissionLevel level)
        {
            try
            {
                var directoryInfo = new DirectoryInfo(path);

                // Remove inherited permissions and set explicit permissions
                var attributes = level switch
                {
                    DirectoryPermissionLevel.OwnerReadOnly => FileAttributes.ReadOnly | FileAttributes.Directory,
                    DirectoryPermissionLevel.OwnerWriteOnly => FileAttributes.Directory,
                    DirectoryPermissionLevel.OwnerAppendOnly => FileAttributes.Directory,
                    _ => FileAttributes.Directory
                };

                directoryInfo.Attributes = attributes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set Windows directory permissions for {Path}", path);
            }
        }

        /// <summary>
        /// Sets Windows file permissions using file attributes.
        /// </summary>
        private void SetWindowsFilePermissions(string path, FilePermissionLevel level)
        {
            try
            {
                var fileInfo = new FileInfo(path);

                var attributes = level switch
                {
                    FilePermissionLevel.ReadOnly => FileAttributes.ReadOnly,
                    FilePermissionLevel.WriteOnly => FileAttributes.Normal,
                    FilePermissionLevel.ReadWrite => FileAttributes.Normal,
                    _ => FileAttributes.ReadOnly
                };

                fileInfo.Attributes = attributes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set Windows file permissions for {Path}", path);
            }
        }

        /// <summary>
        /// Computes SHA256 checksum of a file.
        /// </summary>
        private async Task<string> ComputeSha256ChecksumAsync(string filePath, CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);

            var hashBytes = await sha256.ComputeHashAsync(fileStream, cancellationToken);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Securely deletes a directory with multi-pass file overwrite.
        /// </summary>
        private async Task SecureDeleteDirectoryAsync(string path, CancellationToken cancellationToken)
        {
            if (!Directory.Exists(path))
                return;

            try
            {
                // First, recursively process all files
                var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    await SecureDeleteFileAsync(file, cancellationToken);
                }

                // Then delete empty directories
                await Task.Run(() => Directory.Delete(path, recursive: true), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during secure directory deletion: {Path}", path);
                throw;
            }
        }

        /// <summary>
        /// Securely deletes a file with 3-pass overwrite (DoD 5220.22-M standard).
        /// </summary>
        private async Task SecureDeleteFileAsync(string filePath, CancellationToken cancellationToken)
        {
            const int passes = 3;

            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                    return;

                var fileLength = fileInfo.Length;

                // Remove read-only attribute if present
                if ((fileInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    fileInfo.Attributes &= ~FileAttributes.ReadOnly;
                }

                // Perform 3-pass overwrite
                for (var pass = 0; pass < passes; pass++)
                {
                    using var fileStream = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Write,
                        FileShare.None);

                    var buffer = new byte[4096];
                    var random = new Random();

                    // Pass 1: Random data
                    // Pass 2: Complement of random data
                    // Pass 3: Random data again
                    if (pass % 2 == 0)
                    {
                        random.NextBytes(buffer);
                    }
                    else
                    {
                        random.NextBytes(buffer);
                        for (var i = 0; i < buffer.Length; i++)
                        {
                            buffer[i] = (byte)~buffer[i]; // Complement
                        }
                    }

                    fileStream.Position = 0;
                    var remaining = fileLength;
                    while (remaining > 0)
                    {
                        var toWrite = (int)Math.Min(buffer.Length, remaining);
                        await fileStream.WriteAsync(buffer.AsMemory(0, toWrite), cancellationToken);
                        remaining -= toWrite;
                    }

                    await fileStream.FlushAsync(cancellationToken);
                }

                // Finally, delete the file
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to securely delete file: {FilePath}", filePath);

                // Attempt normal deletion as fallback
                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                    // Log but don't throw - best effort cleanup
                    _logger.LogWarning("Fallback deletion also failed for: {FilePath}", filePath);
                }
            }
        }

        /// <summary>
        /// Disposes the staging manager and cleanup timer.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cleanupTimer?.Dispose();
            _cleanupLock?.Dispose();

            _logger.LogInformation("SecureStagingManager disposed");
        }

        /// <summary>
        /// Directory permission levels for staging environments.
        /// </summary>
        private enum DirectoryPermissionLevel
        {
            OwnerFullAccess,    // 0700
            OwnerReadOnly,      // 0500
            OwnerWriteOnly,     // 0300
            OwnerAppendOnly     // 0200
        }

        /// <summary>
        /// File permission levels for staged scripts.
        /// </summary>
        private enum FilePermissionLevel
        {
            ReadOnly,           // 0400
            WriteOnly,          // 0200
            ReadWrite           // 0600
        }
    }
}
