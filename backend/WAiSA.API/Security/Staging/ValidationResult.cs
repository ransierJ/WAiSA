using System;
using System.Collections.Generic;

namespace WAiSA.API.Security.Staging
{
    /// <summary>
    /// Represents the result of script validation and staging operations.
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the validation was successful.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the SHA256 checksum of the staged script.
        /// </summary>
        public string Checksum { get; }

        /// <summary>
        /// Gets the full path to the staged script file.
        /// </summary>
        public string StagedFilePath { get; }

        /// <summary>
        /// Gets the collection of validation errors, if any.
        /// </summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// Gets the collection of validation warnings, if any.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// Gets the timestamp when the validation was performed.
        /// </summary>
        public DateTimeOffset ValidatedAt { get; }

        /// <summary>
        /// Gets the size of the staged file in bytes.
        /// </summary>
        public long FileSizeBytes { get; }

        private ValidationResult(
            bool isValid,
            string checksum,
            string stagedFilePath,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings,
            long fileSizeBytes)
        {
            IsValid = isValid;
            Checksum = checksum;
            StagedFilePath = stagedFilePath;
            Errors = errors ?? Array.Empty<string>();
            Warnings = warnings ?? Array.Empty<string>();
            ValidatedAt = DateTimeOffset.UtcNow;
            FileSizeBytes = fileSizeBytes;
        }

        /// <summary>
        /// Creates a successful validation result.
        /// </summary>
        /// <param name="checksum">The SHA256 checksum of the file.</param>
        /// <param name="stagedFilePath">The path to the staged file.</param>
        /// <param name="fileSizeBytes">The file size in bytes.</param>
        /// <param name="warnings">Optional warnings.</param>
        /// <returns>A successful validation result.</returns>
        public static ValidationResult Success(
            string checksum,
            string stagedFilePath,
            long fileSizeBytes,
            IReadOnlyList<string> warnings = null)
        {
            return new ValidationResult(
                isValid: true,
                checksum: checksum,
                stagedFilePath: stagedFilePath,
                errors: Array.Empty<string>(),
                warnings: warnings ?? Array.Empty<string>(),
                fileSizeBytes: fileSizeBytes);
        }

        /// <summary>
        /// Creates a failed validation result.
        /// </summary>
        /// <param name="errors">The validation errors.</param>
        /// <returns>A failed validation result.</returns>
        public static ValidationResult Failure(IReadOnlyList<string> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                throw new ArgumentException("At least one error must be provided for a failed validation.", nameof(errors));
            }

            return new ValidationResult(
                isValid: false,
                checksum: null,
                stagedFilePath: null,
                errors: errors,
                warnings: Array.Empty<string>(),
                fileSizeBytes: 0);
        }
    }
}
