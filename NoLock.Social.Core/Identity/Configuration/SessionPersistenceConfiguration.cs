using System.Text.Json;

namespace NoLock.Social.Core.Identity.Configuration
{
    /// <summary>
    /// Configuration for session persistence functionality
    /// </summary>
    public static class SessionPersistenceConfiguration
    {
        /// <summary>
        /// Storage key for encrypted session data in sessionStorage
        /// </summary>
        public const string SessionStorageKey = "nolock_session_v1";

        /// <summary>
        /// Storage key for session metadata in sessionStorage
        /// </summary>
        public const string SessionMetadataKey = "nolock_session_meta_v1";

        /// <summary>
        /// Default session expiry in minutes
        /// </summary>
        public const int DefaultSessionExpiryMinutes = 30;

        /// <summary>
        /// Maximum session expiry in minutes (24 hours)
        /// </summary>
        public const int MaxSessionExpiryMinutes = 1440;

        /// <summary>
        /// Warning threshold in minutes before session expires
        /// </summary>
        public const int SessionExpiryWarningMinutes = 5;

        /// <summary>
        /// Key derivation iterations for session encryption
        /// Higher value = more secure but slower
        /// </summary>
        public const int KeyDerivationIterations = 100000;

        /// <summary>
        /// Salt size in bytes for key derivation
        /// </summary>
        public const int SaltSize = 32;

        /// <summary>
        /// IV size in bytes for AES encryption
        /// </summary>
        public const int IVSize = 16;

        /// <summary>
        /// Key size in bits for AES encryption
        /// </summary>
        public const int AESKeySize = 256;

        /// <summary>
        /// HMAC key size in bytes
        /// </summary>
        public const int HMACKeySize = 32;

        /// <summary>
        /// JSON serialization options for session data
        /// </summary>
        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// Algorithm name for key derivation
        /// </summary>
        public const string KeyDerivationAlgorithm = "PBKDF2";

        /// <summary>
        /// Hash algorithm for PBKDF2
        /// </summary>
        public const string HashAlgorithm = "SHA-256";

        /// <summary>
        /// Encryption algorithm
        /// </summary>
        public const string EncryptionAlgorithm = "AES-GCM";

        /// <summary>
        /// Check if session should use sessionStorage (survives refreshes but not tabs)
        /// or localStorage (survives browser restarts)
        /// </summary>
        public const bool UseSessionStorage = true;

        /// <summary>
        /// Enable automatic session extension on activity
        /// </summary>
        public const bool AutoExtendSession = true;

        /// <summary>
        /// Minutes to extend session on activity
        /// </summary>
        public const int AutoExtendMinutes = 15;
    }
}