namespace NoLock.Social.Core.OCR.Configuration
{
    /// <summary>
    /// Configuration settings for the OCR service
    /// </summary>
    public class OCRServiceConfiguration
    {
        /// <summary>
        /// Base URL for the OCR API endpoint
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// API key for authentication with the OCR service
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Timeout in seconds for OCR service requests
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maximum number of retry attempts for failed requests
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Flag to enable/disable logging for OCR operations
        /// </summary>
        public bool EnableLogging { get; set; } = true;
    }
}