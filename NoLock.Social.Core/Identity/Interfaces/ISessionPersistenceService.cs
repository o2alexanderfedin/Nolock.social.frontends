using NoLock.Social.Core.Cryptography.Interfaces;

namespace NoLock.Social.Core.Identity.Interfaces
{
    /// <summary>
    /// Service for persisting encrypted session data to browser storage.
    /// Enables session survival across page refreshes while maintaining security.
    /// </summary>
    public interface ISessionPersistenceService
    {
        /// <summary>
        /// Persist an encrypted session to secure storage.
        /// The session data is encrypted using a derived key from the user's passphrase.
        /// </summary>
        /// <param name="sessionData">The session data to persist</param>
        /// <param name="encryptionKey">Key derived from user's passphrase for encryption</param>
        /// <param name="expiryMinutes">Optional session expiry time in minutes (default: 30)</param>
        /// <returns>True if persistence succeeded</returns>
        Task<bool> PersistSessionAsync(PersistedSessionData sessionData, byte[] encryptionKey, int expiryMinutes = 30);

        /// <summary>
        /// Restore a persisted session from secure storage.
        /// </summary>
        /// <returns>The encrypted session data if found and valid, null otherwise</returns>
        ValueTask<EncryptedSessionData?> GetPersistedSessionAsync();

        /// <summary>
        /// Decrypt and restore a session using the provided decryption key.
        /// </summary>
        /// <param name="encryptedData">The encrypted session data</param>
        /// <param name="decryptionKey">Key derived from user's passphrase for decryption</param>
        /// <returns>The decrypted session data if successful, null otherwise</returns>
        Task<PersistedSessionData?> DecryptSessionAsync(EncryptedSessionData encryptedData, byte[] decryptionKey);

        /// <summary>
        /// Clear all persisted session data from storage.
        /// </summary>
        Task ClearPersistedSessionAsync();

        /// <summary>
        /// Check if a persisted session exists and is still valid.
        /// </summary>
        /// <returns>True if a valid persisted session exists</returns>
        Task<bool> HasValidPersistedSessionAsync();

        /// <summary>
        /// Extend the expiry time of the current persisted session.
        /// </summary>
        /// <param name="additionalMinutes">Minutes to add to current expiry</param>
        Task ExtendSessionExpiryAsync(int additionalMinutes);

        /// <summary>
        /// Get the remaining time before the persisted session expires.
        /// </summary>
        /// <returns>Remaining time, or TimeSpan.Zero if no valid session</returns>
        Task<TimeSpan> GetRemainingSessionTimeAsync();
    }

    /// <summary>
    /// Data structure for persisted session information.
    /// Contains only non-sensitive session metadata and encrypted key material.
    /// </summary>
    public class PersistedSessionData
    {
        /// <summary>
        /// Unique session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Username associated with the session
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Public key (safe to store)
        /// </summary>
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Encrypted private key data
        /// </summary>
        public byte[] EncryptedPrivateKey { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Salt used for key derivation (unique per session)
        /// </summary>
        public byte[] Salt { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Initialization vector for encryption
        /// </summary>
        public byte[] IV { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// When the session was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the session will expire
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Last activity timestamp
        /// </summary>
        public DateTime LastActivityAt { get; set; }

        /// <summary>
        /// Session state (locked/unlocked)
        /// </summary>
        public SessionState State { get; set; }

        /// <summary>
        /// Version for future compatibility
        /// </summary>
        public int Version { get; set; } = 1;
    }

    /// <summary>
    /// Encrypted session data as stored in browser storage.
    /// </summary>
    public class EncryptedSessionData
    {
        /// <summary>
        /// The encrypted session payload
        /// </summary>
        public string EncryptedPayload { get; set; } = string.Empty;

        /// <summary>
        /// Metadata that can be stored unencrypted
        /// </summary>
        public SessionMetadata Metadata { get; set; } = new();

        /// <summary>
        /// HMAC for integrity verification
        /// </summary>
        public string IntegrityCheck { get; set; } = string.Empty;
    }

    /// <summary>
    /// Unencrypted metadata for quick session validation.
    /// </summary>
    public class SessionMetadata
    {
        /// <summary>
        /// Session identifier
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// When the session expires
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Version for compatibility checking
        /// </summary>
        public int Version { get; set; } = 1;
    }
}