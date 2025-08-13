using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Cryptography.Interfaces
{
    /// <summary>
    /// Reactive version of session state service using Rx.NET observables
    /// </summary>
    public interface IReactiveSessionStateService : IDisposable
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
        /// Observable stream of session state changes
        /// </summary>
        IObservable<SessionStateChangedEventArgs> SessionStateChanges { get; }

        /// <summary>
        /// Observable stream of session state values
        /// </summary>
        IObservable<SessionState> StateStream { get; }

        /// <summary>
        /// Observable stream of remaining time before timeout
        /// </summary>
        IObservable<TimeSpan> RemainingTimeStream { get; }

        /// <summary>
        /// Observable that emits when session is about to timeout (e.g., 1 minute before)
        /// </summary>
        IObservable<TimeSpan> TimeoutWarningStream { get; }

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

        /// <summary>
        /// Extends the current session by resetting the timeout
        /// </summary>
        Task ExtendSessionAsync();
    }
}