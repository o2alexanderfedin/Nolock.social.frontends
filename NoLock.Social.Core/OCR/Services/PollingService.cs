using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Interfaces;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Generic polling service implementation with exponential backoff support.
    /// Thread-safe for use in Blazor WebAssembly single-threaded environment.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the polling operation.</typeparam>
    public class PollingService<TResult> : IPollingService<TResult>
    {
        private readonly ILogger<PollingService<TResult>> _logger;

        /// <summary>
        /// Initializes a new instance of the PollingService class.
        /// </summary>
        /// <param name="logger">Logger for service operations.</param>
        public PollingService(ILogger<PollingService<TResult>> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Polls an operation until a completion condition is met or timeout occurs.
        /// </summary>
        /// <param name="operation">The async operation to poll.</param>
        /// <param name="isComplete">Predicate to determine if the operation is complete.</param>
        /// <param name="configuration">Polling configuration including intervals and timeout.</param>
        /// <param name="cancellation">Cancellation token for the polling operation.</param>
        /// <returns>The final result of the operation.</returns>
        public async Task<TResult> PollAsync(
            Func<CancellationToken, Task<TResult>> operation,
            Func<TResult, bool> isComplete,
            PollingConfiguration configuration,
            CancellationToken cancellation = default)
        {
            return await PollWithProgressAsync(
                operation,
                isComplete,
                null, // No progress callback
                configuration,
                cancellation);
        }

        /// <summary>
        /// Polls an operation with progress reporting until completion or timeout.
        /// </summary>
        /// <param name="operation">The async operation to poll.</param>
        /// <param name="isComplete">Predicate to determine if the operation is complete.</param>
        /// <param name="progressCallback">Callback invoked with each polling result.</param>
        /// <param name="configuration">Polling configuration including intervals and timeout.</param>
        /// <param name="cancellation">Cancellation token for the polling operation.</param>
        /// <returns>The final result of the operation.</returns>
        public async Task<TResult> PollWithProgressAsync(
            Func<CancellationToken, Task<TResult>> operation,
            Func<TResult, bool> isComplete,
            Action<TResult> progressCallback,
            PollingConfiguration configuration,
            CancellationToken cancellation = default)
        {
            // Validate inputs
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            if (isComplete == null)
                throw new ArgumentNullException(nameof(isComplete));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.Validate();

            var stopwatch = Stopwatch.StartNew();
            var currentInterval = configuration.InitialIntervalSeconds;
            var attemptCount = 0;

            _logger.LogInformation(
                "Starting polling operation. Initial interval: {InitialInterval}s, " +
                "Max duration: {MaxDuration}s, Use backoff: {UseBackoff}",
                configuration.InitialIntervalSeconds,
                configuration.MaxPollingDurationSeconds,
                configuration.UseExponentialBackoff);

            // Create a combined cancellation token that includes timeout
            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(configuration.MaxPollingDurationSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation, timeoutCts.Token);

            try
            {
                while (true)
                {
                    attemptCount++;
                    
                    // Check if we've exceeded max attempts
                    if (configuration.MaxAttempts.HasValue && attemptCount > configuration.MaxAttempts.Value)
                    {
                        _logger.LogWarning(
                            "Polling exceeded maximum attempts. Attempts: {Attempts}, Max: {MaxAttempts}",
                            attemptCount, configuration.MaxAttempts.Value);
                        throw new TimeoutException(
                            $"Polling operation exceeded maximum attempts ({configuration.MaxAttempts.Value})");
                    }

                    // Execute the operation
                    _logger.LogDebug(
                        "Executing polling operation. Attempt: {Attempt}, Elapsed: {Elapsed}s",
                        attemptCount, stopwatch.Elapsed.TotalSeconds);

                    TResult result;
                    try
                    {
                        result = await operation(combinedCts.Token);
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                    {
                        _logger.LogWarning(
                            "Polling operation timed out after {Duration}s and {Attempts} attempts",
                            stopwatch.Elapsed.TotalSeconds, attemptCount);
                        throw new TimeoutException(
                            $"Polling operation timed out after {configuration.MaxPollingDurationSeconds} seconds");
                    }

                    // Report progress if callback provided
                    progressCallback?.Invoke(result);

                    // Check if operation is complete
                    if (isComplete(result))
                    {
                        _logger.LogInformation(
                            "Polling operation completed successfully. " +
                            "Total duration: {Duration}s, Attempts: {Attempts}",
                            stopwatch.Elapsed.TotalSeconds, attemptCount);
                        return result;
                    }

                    // Calculate next interval
                    var nextInterval = CalculateNextInterval(currentInterval, configuration);
                    
                    _logger.LogDebug(
                        "Operation not complete. Waiting {Interval}s before next attempt",
                        nextInterval);

                    // Wait before next attempt
                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(nextInterval),
                            combinedCts.Token);
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                    {
                        _logger.LogWarning(
                            "Polling operation timed out during delay after {Duration}s and {Attempts} attempts",
                            stopwatch.Elapsed.TotalSeconds, attemptCount);
                        throw new TimeoutException(
                            $"Polling operation timed out after {configuration.MaxPollingDurationSeconds} seconds");
                    }

                    currentInterval = (int)nextInterval;
                }
            }
            catch (OperationCanceledException) when (!timeoutCts.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Polling operation was cancelled after {Duration}s and {Attempts} attempts",
                    stopwatch.Elapsed.TotalSeconds, attemptCount);
                throw;
            }
            catch (Exception ex) when (!(ex is TimeoutException || ex is OperationCanceledException))
            {
                _logger.LogError(ex,
                    "Polling operation failed after {Duration}s and {Attempts} attempts",
                    stopwatch.Elapsed.TotalSeconds, attemptCount);
                throw;
            }
        }

        /// <summary>
        /// Calculates the next polling interval based on configuration.
        /// </summary>
        /// <param name="currentInterval">The current polling interval in seconds.</param>
        /// <param name="configuration">The polling configuration.</param>
        /// <returns>The next polling interval in seconds.</returns>
        private double CalculateNextInterval(double currentInterval, PollingConfiguration configuration)
        {
            if (!configuration.UseExponentialBackoff)
            {
                return configuration.InitialIntervalSeconds;
            }

            var nextInterval = currentInterval * configuration.BackoffMultiplier;
            return Math.Min(nextInterval, configuration.MaxIntervalSeconds);
        }
    }
}