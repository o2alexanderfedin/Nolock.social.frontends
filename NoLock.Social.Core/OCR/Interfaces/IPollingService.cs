namespace NoLock.Social.Core.OCR.Interfaces
{
    /// <summary>
    /// Defines a generic polling service with exponential backoff support.
    /// Provides a reusable pattern for polling any asynchronous operation.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the polling operation.</typeparam>
    public interface IPollingService<TResult>
    {
        /// <summary>
        /// Polls an operation until a completion condition is met or timeout occurs.
        /// Uses exponential backoff to reduce server load.
        /// </summary>
        /// <param name="operation">The async operation to poll.</param>
        /// <param name="isComplete">Predicate to determine if the operation is complete.</param>
        /// <param name="configuration">Polling configuration including intervals and timeout.</param>
        /// <param name="cancellation">Cancellation token for the polling operation.</param>
        /// <returns>
        /// A task that represents the asynchronous polling operation.
        /// The task result contains the final result of the operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        /// <exception cref="TimeoutException">Thrown when polling exceeds maximum duration.</exception>
        /// <exception cref="OperationCanceledException">Thrown when polling is cancelled.</exception>
        Task<TResult> PollAsync(
            Func<CancellationToken, Task<TResult>> operation,
            Func<TResult, bool> isComplete,
            PollingConfiguration configuration,
            CancellationToken cancellation = default);

        /// <summary>
        /// Polls an operation with progress reporting until completion or timeout.
        /// </summary>
        /// <param name="operation">The async operation to poll.</param>
        /// <param name="isComplete">Predicate to determine if the operation is complete.</param>
        /// <param name="progressCallback">Callback invoked with each polling result.</param>
        /// <param name="configuration">Polling configuration including intervals and timeout.</param>
        /// <param name="cancellation">Cancellation token for the polling operation.</param>
        /// <returns>
        /// A task that represents the asynchronous polling operation.
        /// The task result contains the final result of the operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        /// <exception cref="TimeoutException">Thrown when polling exceeds maximum duration.</exception>
        /// <exception cref="OperationCanceledException">Thrown when polling is cancelled.</exception>
        Task<TResult> PollWithProgressAsync(
            Func<CancellationToken, Task<TResult>> operation,
            Func<TResult, bool> isComplete,
            Action<TResult> progressCallback,
            PollingConfiguration configuration,
            CancellationToken cancellation = default);
    }

    /// <summary>
    /// Configuration for polling operations with exponential backoff.
    /// </summary>
    public class PollingConfiguration
    {
        /// <summary>
        /// Initial polling interval in seconds.
        /// Default: 5 seconds.
        /// </summary>
        public int InitialIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// Maximum polling interval in seconds.
        /// Default: 30 seconds.
        /// </summary>
        public int MaxIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Backoff multiplier for exponential increase.
        /// Default: 1.5 (50% increase each time).
        /// </summary>
        public double BackoffMultiplier { get; set; } = 1.5;

        /// <summary>
        /// Maximum total polling duration in seconds.
        /// Default: 300 seconds (5 minutes).
        /// </summary>
        public int MaxPollingDurationSeconds { get; set; } = 300;

        /// <summary>
        /// Whether to use exponential backoff or fixed intervals.
        /// Default: true.
        /// </summary>
        public bool UseExponentialBackoff { get; set; } = true;

        /// <summary>
        /// Maximum number of polling attempts.
        /// Null means no limit (use MaxPollingDurationSeconds instead).
        /// Default: null.
        /// </summary>
        public int? MaxAttempts { get; set; }

        /// <summary>
        /// Gets the predefined polling configuration for OCR operations.
        /// Uses: 5s, 10s, 15s, 30s intervals with 5-minute max duration.
        /// </summary>
        public static PollingConfiguration OCRDefault => new PollingConfiguration
        {
            InitialIntervalSeconds = 5,
            MaxIntervalSeconds = 30,
            BackoffMultiplier = 2.0, // Double each time: 5s -> 10s -> 20s -> 30s
            MaxPollingDurationSeconds = 300, // 5 minutes
            UseExponentialBackoff = true
        };

        /// <summary>
        /// Gets a fast polling configuration for quick operations.
        /// Uses: 1s, 2s, 4s, 8s intervals with 1-minute max duration.
        /// </summary>
        public static PollingConfiguration Fast => new PollingConfiguration
        {
            InitialIntervalSeconds = 1,
            MaxIntervalSeconds = 8,
            BackoffMultiplier = 2.0,
            MaxPollingDurationSeconds = 60,
            UseExponentialBackoff = true
        };

        /// <summary>
        /// Gets a slow polling configuration for long-running operations.
        /// Uses: 30s fixed intervals with 30-minute max duration.
        /// </summary>
        public static PollingConfiguration Slow => new PollingConfiguration
        {
            InitialIntervalSeconds = 30,
            MaxIntervalSeconds = 30,
            BackoffMultiplier = 1.0,
            MaxPollingDurationSeconds = 1800, // 30 minutes
            UseExponentialBackoff = false
        };

        /// <summary>
        /// Validates the configuration settings.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when configuration is invalid.</exception>
        public void Validate()
        {
            if (InitialIntervalSeconds <= 0)
                throw new ArgumentOutOfRangeException("Initial interval must be greater than 0", nameof(InitialIntervalSeconds));

            if (MaxIntervalSeconds < InitialIntervalSeconds)
                throw new ArgumentOutOfRangeException("Max interval must be greater than or equal to initial interval", nameof(MaxIntervalSeconds));

            if (BackoffMultiplier < 1.0)
                throw new ArgumentOutOfRangeException("Backoff multiplier must be at least 1.0", nameof(BackoffMultiplier));

            if (MaxPollingDurationSeconds <= 0)
                throw new ArgumentOutOfRangeException("Max polling duration must be greater than 0", nameof(MaxPollingDurationSeconds));

            if (MaxAttempts.HasValue && MaxAttempts.Value <= 0)
                throw new ArgumentOutOfRangeException("Max attempts must be greater than 0 if specified", nameof(MaxAttempts));
        }
    }
}