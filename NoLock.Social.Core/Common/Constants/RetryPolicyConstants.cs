namespace NoLock.Social.Core.Common.Constants
{
    /// <summary>
    /// Constants for retry policy configuration across the application.
    /// </summary>
    public static class RetryPolicyConstants
    {
        /// <summary>
        /// Default maximum number of retry attempts for transient failures.
        /// </summary>
        public const int DefaultMaxRetryAttempts = 3;

        /// <summary>
        /// Initial delay in milliseconds before the first retry attempt.
        /// </summary>
        public const int DefaultInitialDelayMs = 1000;

        /// <summary>
        /// Maximum delay in milliseconds between retry attempts to prevent excessive waiting.
        /// </summary>
        public const int DefaultMaxDelayMs = 30000;

        /// <summary>
        /// Multiplier for exponential backoff calculation between retry attempts.
        /// </summary>
        public const double DefaultBackoffMultiplier = 2.0;

        /// <summary>
        /// Jitter percentage to add randomization to retry delays (prevents thundering herd).
        /// </summary>
        public const double JitterPercentage = 0.2;

        /// <summary>
        /// Minimum delay in milliseconds to ensure there's always some delay.
        /// </summary>
        public const int MinimumDelayMs = 1;

        /// <summary>
        /// Value representing half for jitter calculation centering.
        /// </summary>
        public const double JitterCenterValue = 0.5;

        /// <summary>
        /// Multiplier for converting jitter range to full range.
        /// </summary>
        public const int JitterRangeMultiplier = 2;
    }
}