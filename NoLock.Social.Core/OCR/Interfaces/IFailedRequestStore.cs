using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NoLock.Social.Core.Common.Results;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Interfaces
{
    /// <summary>
    /// Interface for storing and retrieving failed OCR requests for retry.
    /// Provides persistent storage for requests that failed due to transient issues.
    /// </summary>
    public interface IFailedRequestStore
    {
        /// <summary>
        /// Stores a failed OCR request for later retry.
        /// </summary>
        /// <param name="request">The failed request to store.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The unique identifier for the stored request.</returns>
        Task<string> StoreFailedRequestAsync(
            FailedOCRRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all failed requests that are eligible for retry.
        /// </summary>
        /// <param name="maxRetryCount">Maximum retry count to filter by.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>List of failed requests eligible for retry.</returns>
        Task<IReadOnlyList<FailedOCRRequest>> GetRetryableRequestsAsync(
            int maxRetryCount = 5,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a specific failed request by ID.
        /// </summary>
        /// <param name="requestId">The unique identifier of the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The failed request if found; otherwise, null.</returns>
        Task<Result<FailedOCRRequest?>> GetRequestAsync(
            string requestId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the retry count and last attempt time for a failed request.
        /// </summary>
        /// <param name="requestId">The unique identifier of the request.</param>
        /// <param name="incrementRetryCount">Whether to increment the retry count.</param>
        /// <param name="errorMessage">Optional error message from the last attempt.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if the request was updated; otherwise, false.</returns>
        Task<bool> UpdateRetryStatusAsync(
            string requestId,
            bool incrementRetryCount = true,
            string? errorMessage = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a request from the failed store (typically after successful retry).
        /// </summary>
        /// <param name="requestId">The unique identifier of the request.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if the request was removed; otherwise, false.</returns>
        Task<bool> RemoveRequestAsync(
            string requestId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes all requests that have exceeded the maximum retry count.
        /// </summary>
        /// <param name="maxRetryCount">The maximum retry count threshold.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The number of requests removed.</returns>
        Task<int> RemoveExhaustedRequestsAsync(
            int maxRetryCount = 5,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the count of pending failed requests.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The count of pending requests.</returns>
        Task<int> GetPendingCountAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all failed requests from the store.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The number of requests cleared.</returns>
        Task<int> ClearAllAsync(
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a failed OCR request stored for retry.
    /// </summary>
    public class FailedOCRRequest
    {
        /// <summary>
        /// Unique identifier for the failed request.
        /// </summary>
        public string RequestId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The original OCR submission request.
        /// </summary>
        public OCRSubmissionRequest OriginalRequest { get; set; } = new();

        /// <summary>
        /// Timestamp when the request first failed.
        /// </summary>
        public DateTime FailedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp of the last retry attempt.
        /// </summary>
        public DateTime? LastRetryAt { get; set; }

        /// <summary>
        /// Number of retry attempts made.
        /// </summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// The type of failure that caused the request to fail.
        /// </summary>
        public FailureType FailureType { get; set; } = FailureType.Unknown;

        /// <summary>
        /// Error message from the last failure.
        /// </summary>
        public string? LastErrorMessage { get; set; }

        /// <summary>
        /// HTTP status code from the last failure, if applicable.
        /// </summary>
        public int? LastHttpStatusCode { get; set; }

        /// <summary>
        /// Priority for retry (lower values = higher priority).
        /// </summary>
        public int Priority { get; set; } = 5;

        /// <summary>
        /// Metadata associated with the request.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Calculates the next retry time based on exponential backoff.
        /// </summary>
        /// <param name="baseDelaySeconds">Base delay in seconds.</param>
        /// <param name="maxDelaySeconds">Maximum delay in seconds.</param>
        /// <returns>The next retry time.</returns>
        public DateTime CalculateNextRetryTime(
            int baseDelaySeconds = 60,
            int maxDelaySeconds = 3600)
        {
            var delaySeconds = Math.Min(
                baseDelaySeconds * Math.Pow(2, RetryCount),
                maxDelaySeconds);
            
            var lastAttempt = LastRetryAt ?? FailedAt;
            return lastAttempt.AddSeconds(delaySeconds);
        }

        /// <summary>
        /// Determines if the request is eligible for retry.
        /// </summary>
        /// <param name="maxRetryCount">Maximum allowed retry count.</param>
        /// <param name="currentTime">Current time for comparison.</param>
        /// <returns>True if eligible for retry; otherwise, false.</returns>
        public bool IsEligibleForRetry(int maxRetryCount = 5, DateTime? currentTime = null)
        {
            if (RetryCount >= maxRetryCount)
                return false;

            if (FailureType == FailureType.Permanent)
                return false;

            var now = currentTime ?? DateTime.UtcNow;
            var nextRetryTime = CalculateNextRetryTime();
            
            return now >= nextRetryTime;
        }
    }
}