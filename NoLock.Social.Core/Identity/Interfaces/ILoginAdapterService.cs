using System;
using System.Threading.Tasks;
using NoLock.Social.Core.Identity.Models;

namespace NoLock.Social.Core.Identity.Interfaces
{
    /// <summary>
    /// Adapter service that provides login semantics over the existing identity unlock mechanism.
    /// This service wraps the core cryptographic identity system with user-friendly login/logout concepts.
    /// </summary>
    public interface ILoginAdapterService
    {
        /// <summary>
        /// Current login state including whether user is logged in, locked, and user details.
        /// </summary>
        LoginState CurrentLoginState { get; }
        
        /// <summary>
        /// Observable stream of login state changes for reactive UI updates.
        /// Components can subscribe to this to automatically update when login state changes.
        /// </summary>
        IObservable<LoginStateChange> LoginStateChanges { get; }
        
        /// <summary>
        /// Perform login operation. This wraps identity unlock with user tracking
        /// to determine if the user is new or returning.
        /// </summary>
        /// <param name="username">The username for identity derivation</param>
        /// <param name="passphrase">The passphrase for key derivation (never stored)</param>
        /// <param name="rememberUsername">Whether to remember the username for next login</param>
        /// <returns>Result containing success status, user info, and session details</returns>
        Task<LoginResult> LoginAsync(string username, string passphrase, bool rememberUsername);
        
        /// <summary>
        /// Logout completely, ending the session and clearing all sensitive data from memory.
        /// This is different from Lock which keeps the session but requires re-authentication.
        /// </summary>
        /// <returns>Task representing the asynchronous operation</returns>
        Task LogoutAsync();
        
        /// <summary>
        /// Lock the current session. This keeps keys in memory but requires passphrase
        /// to unlock again. Used for temporary security without full logout.
        /// </summary>
        /// <returns>Task representing the asynchronous operation</returns>
        Task LockAsync();
        
        /// <summary>
        /// Check if the current user is new or returning based on their content history.
        /// </summary>
        /// <returns>True if the user has existing content, false if they are new</returns>
        Task<bool> IsReturningUserAsync();
    }
}