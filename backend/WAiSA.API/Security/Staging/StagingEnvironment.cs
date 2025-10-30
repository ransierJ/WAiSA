using System;

namespace WAiSA.API.Security.Staging
{
    /// <summary>
    /// Represents an isolated staging environment for AI-generated scripts.
    /// </summary>
    public sealed class StagingEnvironment : IDisposable
    {
        /// <summary>
        /// Gets the unique identifier for the agent.
        /// </summary>
        public string AgentId { get; }

        /// <summary>
        /// Gets the unique identifier for the session.
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// Gets the root path of the staging environment.
        /// </summary>
        public string RootPath { get; }

        /// <summary>
        /// Gets the path to the scripts directory (read-only after write).
        /// </summary>
        public string ScriptsPath { get; }

        /// <summary>
        /// Gets the path to the inputs directory (read-only).
        /// </summary>
        public string InputsPath { get; }

        /// <summary>
        /// Gets the path to the outputs directory (write-only).
        /// </summary>
        public string OutputsPath { get; }

        /// <summary>
        /// Gets the path to the logs directory (append-only).
        /// </summary>
        public string LogsPath { get; }

        /// <summary>
        /// Gets the timestamp when this environment was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; }

        /// <summary>
        /// Gets the timestamp when this environment expires and should be cleaned up.
        /// </summary>
        public DateTimeOffset ExpiresAt { get; }

        /// <summary>
        /// Gets a value indicating whether this environment has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        private readonly IStagingManager _stagingManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="StagingEnvironment"/> class.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="rootPath">The root path of the staging environment.</param>
        /// <param name="scriptsPath">The scripts directory path.</param>
        /// <param name="inputsPath">The inputs directory path.</param>
        /// <param name="outputsPath">The outputs directory path.</param>
        /// <param name="logsPath">The logs directory path.</param>
        /// <param name="expiresAt">The expiration timestamp.</param>
        /// <param name="stagingManager">The staging manager for cleanup operations.</param>
        public StagingEnvironment(
            string agentId,
            string sessionId,
            string rootPath,
            string scriptsPath,
            string inputsPath,
            string outputsPath,
            string logsPath,
            DateTimeOffset expiresAt,
            IStagingManager stagingManager)
        {
            AgentId = agentId ?? throw new ArgumentNullException(nameof(agentId));
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            RootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            ScriptsPath = scriptsPath ?? throw new ArgumentNullException(nameof(scriptsPath));
            InputsPath = inputsPath ?? throw new ArgumentNullException(nameof(inputsPath));
            OutputsPath = outputsPath ?? throw new ArgumentNullException(nameof(outputsPath));
            LogsPath = logsPath ?? throw new ArgumentNullException(nameof(logsPath));
            ExpiresAt = expiresAt;
            CreatedAt = DateTimeOffset.UtcNow;
            _stagingManager = stagingManager ?? throw new ArgumentNullException(nameof(stagingManager));
        }

        /// <summary>
        /// Disposes the staging environment and triggers cleanup.
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                // Trigger async cleanup without waiting
                _ = _stagingManager.CleanupStagingEnvironmentAsync(this);
            }
        }

        /// <summary>
        /// Determines whether the environment has expired.
        /// </summary>
        /// <returns>True if expired; otherwise, false.</returns>
        public bool IsExpired() => DateTimeOffset.UtcNow >= ExpiresAt;
    }
}
