using System;
using System.Threading;
using System.Threading.Tasks;

namespace WAiSA.API.Security.Staging
{
    /// <summary>
    /// Defines the contract for managing secure staging environments for AI-generated scripts.
    /// </summary>
    public interface IStagingManager
    {
        /// <summary>
        /// Creates an isolated staging environment with strict permissions and auto-cleanup.
        /// </summary>
        /// <param name="agentId">Unique identifier for the agent.</param>
        /// <param name="sessionId">Unique identifier for the session.</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>A staging environment with isolated directory structure.</returns>
        Task<StagingEnvironment> CreateStagingEnvironmentAsync(
            string agentId,
            string sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates and stages a script in the secure environment.
        /// </summary>
        /// <param name="environment">The staging environment to use.</param>
        /// <param name="content">The script content to validate and stage.</param>
        /// <param name="name">The script filename.</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>Validation result with checksum and status.</returns>
        Task<ValidationResult> ValidateAndStageScriptAsync(
            StagingEnvironment environment,
            string content,
            string name,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Securely cleans up a staging environment with multi-pass file overwrite.
        /// </summary>
        /// <param name="environment">The staging environment to clean up.</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>Task representing the async operation.</returns>
        Task CleanupStagingEnvironmentAsync(
            StagingEnvironment environment,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the base staging path for the current platform.
        /// </summary>
        string GetBaseStagingPath();
    }
}
