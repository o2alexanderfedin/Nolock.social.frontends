using System.Text.Json;

namespace NoLock.Social.Core.Identity.Configuration
{
    /// <summary>
    /// Configuration constants for the login system.
    /// Centralizes magic numbers and configuration values for maintainability.
    /// </summary>
    public static class LoginConfiguration
    {
        /// <summary>
        /// Number of days after which remembered username expires
        /// </summary>
        public const int RememberMeExpiryDays = 30;
        
        /// <summary>
        /// LocalStorage key for remembered user data
        /// </summary>
        public const string StorageKey = "nolock_remembered_user";
        
        /// <summary>
        /// Maximum allowed username length
        /// </summary>
        public const int MaxUsernameLength = 50;
        
        /// <summary>
        /// Minimum allowed username length
        /// </summary>
        public const int MinUsernameLength = 3;
        
        /// <summary>
        /// Minimum allowed passphrase length for security
        /// </summary>
        public const int MinPassphraseLength = 12;
        
        /// <summary>
        /// Maximum allowed passphrase length to prevent abuse
        /// </summary>
        public const int MaxPassphraseLength = 200;
        
        /// <summary>
        /// Pattern for allowed username characters (alphanumeric, hyphens, underscores)
        /// </summary>
        public const string UsernamePattern = @"^[a-zA-Z0-9_-]+$";
        
        /// <summary>
        /// Default JSON serialization options for localStorage operations
        /// </summary>
        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
}