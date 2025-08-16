using System;
using System.Threading;
using System.Threading.Tasks;

namespace NoLock.Social.Core.OCR.Interfaces
{
    /// <summary>
    /// Defines a retry policy for handling transient failures in operations.
    /// Provides configurable retry strategies with exponential backoff support.
    /// </summary>
    public interface IRetryPolicy
    {
        /// <summary>
        /// Gets the maximum number of retry attempts.
        /// </summary>
        int MaxAttempts { get; }

        /// <summary>
        /// Gets the initial delay between retry attempts in milliseconds.
        /// </summary>
        int InitialDelayMs { get; }

        /// <summary>
        /// Gets the maximum delay between retry attempts in milliseconds.
        /// </summary>
        int MaxDelayMs { get; }

        /// <summary>
        /// Gets the backoff multiplier for exponential backoff.
        /// </summary>
        double BackoffMultiplier { get; }

        /// <summary>
        /// Determines if an exception should trigger a retry.
        /// </summary>
        /// <param name="exception">The exception to evaluate.</param>
        /// <returns>True if the operation should be retried; otherwise, false.</returns>
        bool ShouldRetry(Exception exception);

        /// <summary>
        /// Calculates the delay before the next retry attempt.
        /// </summary>
        /// <param name="attemptNumber">The current attempt number (1-based).</param>
        /// <returns>The delay in milliseconds before the next retry.</returns>
        int CalculateDelay(int attemptNumber);

        /// <summary>
        /// Executes an operation with retry logic based on the policy.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The result of the successful operation.</returns>
        /// <exception cref="AggregateException">Thrown when all retry attempts fail.</exception>
        Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an operation with retry logic and progress reporting.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="onRetry">Callback invoked before each retry attempt.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The result of the successful operation.</returns>
        /// <exception cref="AggregateException">Thrown when all retry attempts fail.</exception>
        Task<TResult> ExecuteWithCallbackAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            Action<int, Exception, int> onRetry,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the classification of a failure for retry purposes.
    /// </summary>
    public enum FailureType
    {
        /// <summary>
        /// Transient failure that should be retried (e.g., network timeout).
        /// </summary>
        Transient,

        /// <summary>
        /// Permanent failure that should not be retried (e.g., invalid data).
        /// </summary>
        Permanent,

        /// <summary>
        /// Unknown failure type, use default retry policy.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Interface for classifying failures to determine retry behavior.
    /// </summary>
    public interface IFailureClassifier
    {
        /// <summary>
        /// Classifies an exception to determine if it represents a transient or permanent failure.
        /// </summary>
        /// <param name="exception">The exception to classify.</param>
        /// <returns>The classification of the failure.</returns>
        FailureType Classify(Exception exception);
    }
}