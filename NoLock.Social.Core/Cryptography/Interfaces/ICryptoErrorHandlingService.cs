namespace NoLock.Social.Core.Cryptography.Interfaces
{
    /// <summary>
    /// Service for handling cryptographic errors with proper categorization and logging
    /// </summary>
    public interface ICryptoErrorHandlingService
    {
        /// <summary>
        /// Handle an error with proper categorization and logging
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="context">Context about where the error occurred</param>
        /// <returns>Error information with user-friendly message and recovery suggestions</returns>
        Task<ErrorInfo> HandleErrorAsync(Exception exception, ErrorContext context);

        /// <summary>
        /// Get a user-friendly error message for a category
        /// </summary>
        /// <param name="category">The error category</param>
        /// <returns>User-friendly message</returns>
        Task<string> GetUserFriendlyMessageAsync(ErrorCategory category);

        /// <summary>
        /// Get recovery suggestions for an error category
        /// </summary>
        /// <param name="category">The error category</param>
        /// <returns>List of recovery suggestions</returns>
        Task<IReadOnlyList<string>> GetRecoverySuggestionsAsync(ErrorCategory category);

        /// <summary>
        /// Log error information without sensitive data
        /// </summary>
        /// <param name="errorInfo">The error information to log</param>
        Task LogErrorAsync(ErrorInfo errorInfo);

        /// <summary>
        /// Sanitize data for logging by removing sensitive information
        /// </summary>
        /// <param name="data">Data to sanitize</param>
        /// <returns>Sanitized data safe for logging</returns>
        Task<Dictionary<string, object>> SanitizeForLoggingAsync(Dictionary<string, object> data);
    }

    /// <summary>
    /// Error categories for cryptographic operations
    /// </summary>
    public enum ErrorCategory
    {
        Unknown,
        KeyDerivation,
        SignatureGeneration,
        SignatureVerification,
        SessionManagement,
        Storage,
        Memory,
        Initialization,
        BrowserCompatibility
    }

    /// <summary>
    /// Context information about where an error occurred
    /// </summary>
    public class ErrorContext
    {
        /// <summary>
        /// The operation being performed when the error occurred
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// The component where the error occurred
        /// </summary>
        public string Component { get; set; } = string.Empty;

        /// <summary>
        /// Additional context data (will be sanitized before logging)
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }
    }

    /// <summary>
    /// Information about a handled error
    /// </summary>
    public class ErrorInfo
    {
        /// <summary>
        /// Error category
        /// </summary>
        public ErrorCategory Category { get; set; }

        /// <summary>
        /// User-friendly error message
        /// </summary>
        public string UserMessage { get; set; } = string.Empty;

        /// <summary>
        /// Technical details for debugging (sanitized)
        /// </summary>
        public string TechnicalDetails { get; set; } = string.Empty;

        /// <summary>
        /// Recovery suggestions for the user
        /// </summary>
        public IReadOnlyList<string> RecoverySuggestions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Whether this is a critical error requiring immediate attention
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Error code for tracking
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}