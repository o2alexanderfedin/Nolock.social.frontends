namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents the result of a wake lock operation (acquire or release).
    /// Contains success status and detailed error information for troubleshooting.
    /// </summary>
    public class WakeLockResult
    {
        /// <summary>
        /// Indicates whether the wake lock operation was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// The type of wake lock operation that was performed.
        /// </summary>
        public WakeLockOperationType OperationType { get; set; }

        /// <summary>
        /// Error message if the operation failed, null if successful.
        /// Contains browser-specific error details for debugging.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Detailed error information including browser compatibility issues.
        /// Useful for troubleshooting wake lock problems across different browsers.
        /// </summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// Timestamp when the wake lock operation was performed.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Indicates whether the browser supports the Wake Lock API.
        /// </summary>
        public bool IsBrowserSupported { get; set; }

        /// <summary>
        /// Creates a successful wake lock result.
        /// </summary>
        /// <param name="operationType">The type of operation that succeeded.</param>
        /// <returns>A successful WakeLockResult instance.</returns>
        public static WakeLockResult Success(WakeLockOperationType operationType)
        {
            return new WakeLockResult
            {
                IsSuccess = true,
                OperationType = operationType,
                Timestamp = DateTime.UtcNow,
                IsBrowserSupported = true
            };
        }

        /// <summary>
        /// Creates a failed wake lock result with error information.
        /// </summary>
        /// <param name="operationType">The type of operation that failed.</param>
        /// <param name="errorMessage">The primary error message.</param>
        /// <param name="errorDetails">Additional error details for debugging.</param>
        /// <param name="isBrowserSupported">Whether the browser supports Wake Lock API.</param>
        /// <returns>A failed WakeLockResult instance.</returns>
        public static WakeLockResult Failure(
            WakeLockOperationType operationType,
            string errorMessage,
            string? errorDetails = null,
            bool isBrowserSupported = true)
        {
            return new WakeLockResult
            {
                IsSuccess = false,
                OperationType = operationType,
                ErrorMessage = errorMessage,
                ErrorDetails = errorDetails,
                Timestamp = DateTime.UtcNow,
                IsBrowserSupported = isBrowserSupported
            };
        }
    }

    /// <summary>
    /// Enumeration of wake lock operation types.
    /// </summary>
    public enum WakeLockOperationType
    {
        /// <summary>
        /// Wake lock acquisition operation.
        /// </summary>
        Acquire = 0,

        /// <summary>
        /// Wake lock release operation.
        /// </summary>
        Release = 1
    }
}