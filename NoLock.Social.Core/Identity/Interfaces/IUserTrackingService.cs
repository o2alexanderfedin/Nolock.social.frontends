using System;
using System.Threading.Tasks;
using NoLock.Social.Core.Identity.Models;

namespace NoLock.Social.Core.Identity.Interfaces
{
    /// <summary>
    /// Service for tracking user identity existence and history.
    /// Determines if a user is new or returning based on their content history.
    /// </summary>
    public interface IUserTrackingService
    {
        /// <summary>
        /// Check if a public key has been used before by looking for existing content.
        /// This is the primary method to determine if a user is new or returning.
        /// </summary>
        /// <param name="publicKeyBase64">The base64-encoded public key to check</param>
        /// <returns>Information about the user's existence and activity</returns>
        Task<UserTrackingInfo> CheckUserExistsAsync(string publicKeyBase64);
        
        /// <summary>
        /// Mark a user as having created content. This is automatically called
        /// when a user saves their first piece of content.
        /// </summary>
        /// <param name="publicKeyBase64">The base64-encoded public key of the user</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task MarkUserAsActiveAsync(string publicKeyBase64);
        
        /// <summary>
        /// Get a comprehensive summary of user activity including content count,
        /// storage usage, and recent activity timestamps.
        /// </summary>
        /// <param name="publicKeyBase64">The base64-encoded public key to query</param>
        /// <returns>Detailed summary of the user's activity</returns>
        Task<UserActivitySummary> GetUserActivityAsync(string publicKeyBase64);
    }
}