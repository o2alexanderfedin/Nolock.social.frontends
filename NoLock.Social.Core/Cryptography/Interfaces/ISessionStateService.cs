using System;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Cryptography.Interfaces
{
    /// <summary>
    /// Service for managing cryptographic identity session state
    /// </summary>
    public interface ISessionStateService : IDisposable
    {
        /// <summary>
        /// Gets the current session state
        /// </summary>
        SessionState CurrentState { get; }

        /// <summary>
        /// Gets the current identity session (null if locked)
        /// </summary>
        IdentitySession? CurrentSession { get; }

        /// <summary>
        /// Gets whether the session is currently unlocked
        /// </summary>
        bool IsUnlocked { get; }

        /// <summary>
        /// Gets the session timeout in minutes
        /// </summary>
        int SessionTimeoutMinutes { get; set; }

        /// <summary>
        /// Event raised when session state changes
        /// </summary>
        event EventHandler<SessionStateChangedEventArgs>? SessionStateChanged;

        /// <summary>
        /// Starts a new session with the provided identity
        /// </summary>
        Task<bool> StartSessionAsync(string username, Ed25519KeyPair keyPair, ISecureBuffer privateKeyBuffer);

        /// <summary>
        /// Locks the current session (keeps it in memory but requires unlock)
        /// </summary>
        Task LockSessionAsync();

        /// <summary>
        /// Unlocks a locked session with verification
        /// </summary>
        Task<bool> UnlockSessionAsync(string passphrase);

        /// <summary>
        /// Ends the current session and clears all sensitive data
        /// </summary>
        Task EndSessionAsync();

        /// <summary>
        /// Updates the last activity time to prevent timeout
        /// </summary>
        void UpdateActivity();

        /// <summary>
        /// Checks if the session has timed out and locks/ends it if necessary
        /// </summary>
        Task CheckTimeoutAsync();

        /// <summary>
        /// Gets the remaining time before session timeout
        /// </summary>
        TimeSpan GetRemainingTime();
    }
}