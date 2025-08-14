using System.Threading.Tasks;

namespace NoLock.Social.Core.Identity.Interfaces
{
    /// <summary>
    /// Service for handling "Remember Me" functionality.
    /// IMPORTANT: This service only stores username for convenience.
    /// Never stores passphrase, keys, or any sensitive session data.
    /// </summary>
    public interface IRememberMeService
    {
        /// <summary>
        /// Remember username for convenience. This is purely a UX feature
        /// and has no security implications as only the username is stored.
        /// </summary>
        /// <param name="username">The username to remember</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task RememberUsernameAsync(string username);
        
        /// <summary>
        /// Get the remembered username if available. Returns null if no
        /// username is remembered or if the stored data is corrupted.
        /// </summary>
        /// <returns>The remembered username or null</returns>
        Task<string?> GetRememberedUsernameAsync();
        
        /// <summary>
        /// Clear all remembered data. This includes the username and any
        /// non-sensitive preferences that might be stored.
        /// </summary>
        /// <returns>Task representing the asynchronous operation</returns>
        Task ClearRememberedDataAsync();
        
        /// <summary>
        /// Check if a username is currently remembered.
        /// </summary>
        bool IsUsernameRemembered { get; }
    }
}