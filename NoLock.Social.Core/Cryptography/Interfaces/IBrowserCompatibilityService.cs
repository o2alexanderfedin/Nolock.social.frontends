namespace NoLock.Social.Core.Cryptography.Interfaces
{
    /// <summary>
    /// Service for checking browser compatibility with required cryptographic APIs.
    /// </summary>
    public interface IBrowserCompatibilityService
    {
        /// <summary>
        /// Checks if the Web Crypto API is available in the current browser.
        /// </summary>
        /// <returns>True if Web Crypto API is available, false otherwise.</returns>
        Task<bool> IsWebCryptoAvailableAsync();

        /// <summary>
        /// Checks if the browser is running in a secure context (HTTPS).
        /// </summary>
        /// <returns>True if running in secure context, false otherwise.</returns>
        Task<bool> IsSecureContextAsync();

        /// <summary>
        /// Gets detailed browser compatibility information.
        /// </summary>
        /// <returns>Browser compatibility details.</returns>
        Task<BrowserCompatibilityInfo> GetCompatibilityInfoAsync();
    }

    /// <summary>
    /// Browser compatibility information.
    /// </summary>
    public class BrowserCompatibilityInfo
    {
        public bool IsWebCryptoAvailable { get; set; }
        public bool IsSecureContext { get; set; }
        public bool IsCompatible => IsWebCryptoAvailable && IsSecureContext;
        public string BrowserName { get; set; } = string.Empty;
        public string BrowserVersion { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}