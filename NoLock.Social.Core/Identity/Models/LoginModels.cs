using System;
using System.Collections.Generic;
using NoLock.Social.Core.Cryptography.Interfaces;

namespace NoLock.Social.Core.Identity.Models
{
    /// <summary>
    /// Information about a tracked user, including their existence status and activity history.
    /// </summary>
    public class UserTrackingInfo
    {
        /// <summary>
        /// Whether this user has existing content (returning user).
        /// </summary>
        public bool Exists { get; set; }
        
        /// <summary>
        /// When this user was first seen (first content created).
        /// </summary>
        public DateTime? FirstSeen { get; set; }
        
        /// <summary>
        /// When this user was last active (most recent content).
        /// </summary>
        public DateTime? LastSeen { get; set; }
        
        /// <summary>
        /// Total number of content items created by this user.
        /// </summary>
        public int ContentCount { get; set; }
        
        /// <summary>
        /// The user's public key in base64 format.
        /// </summary>
        public string PublicKeyBase64 { get; set; } = string.Empty;
    }

    /// <summary>
    /// Comprehensive summary of user activity and storage usage.
    /// </summary>
    public class UserActivitySummary
    {
        /// <summary>
        /// Total number of content items created.
        /// </summary>
        public int TotalContent { get; set; }
        
        /// <summary>
        /// Timestamp of most recent activity.
        /// </summary>
        public DateTime? LastActivity { get; set; }
        
        /// <summary>
        /// Total storage space used in bytes.
        /// </summary>
        public long TotalStorageBytes { get; set; }
        
        /// <summary>
        /// List of recent content addresses for quick access.
        /// </summary>
        public List<string> RecentContentAddresses { get; set; } = new();
    }

    /// <summary>
    /// Result of a login attempt with all relevant information.
    /// </summary>
    public class LoginResult
    {
        /// <summary>
        /// Whether the login was successful.
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Whether this is a new user (no existing content).
        /// </summary>
        public bool IsNewUser { get; set; }
        
        /// <summary>
        /// Error message if login failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// The established identity session if login succeeded.
        /// </summary>
        public IdentitySession? Session { get; set; }
        
        /// <summary>
        /// User tracking information including activity history.
        /// </summary>
        public UserTrackingInfo? UserInfo { get; set; }
    }

    /// <summary>
    /// Current login state of the application.
    /// </summary>
    public class LoginState
    {
        /// <summary>
        /// Whether a user is currently logged in.
        /// </summary>
        public bool IsLoggedIn { get; set; }
        
        /// <summary>
        /// Whether the current session is locked (requires passphrase to unlock).
        /// </summary>
        public bool IsLocked { get; set; }
        
        /// <summary>
        /// The logged-in username.
        /// </summary>
        public string? Username { get; set; }
        
        /// <summary>
        /// The user's public key in base64 format.
        /// </summary>
        public string? PublicKeyBase64 { get; set; }
        
        /// <summary>
        /// When the user logged in.
        /// </summary>
        public DateTime? LoginTime { get; set; }
        
        /// <summary>
        /// Last activity timestamp for session timeout tracking.
        /// </summary>
        public DateTime? LastActivity { get; set; }
        
        /// <summary>
        /// Whether this is a new user (first time login).
        /// </summary>
        public bool IsNewUser { get; set; }
    }

    /// <summary>
    /// Event representing a change in login state.
    /// </summary>
    public class LoginStateChange
    {
        /// <summary>
        /// The login state before the change.
        /// </summary>
        public LoginState PreviousState { get; set; } = new();
        
        /// <summary>
        /// The new login state after the change.
        /// </summary>
        public LoginState NewState { get; set; } = new();
        
        /// <summary>
        /// The reason for the state change.
        /// </summary>
        public LoginStateChangeReason Reason { get; set; }
        
        /// <summary>
        /// When the state change occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Reasons for login state changes.
    /// </summary>
    public enum LoginStateChangeReason
    {
        /// <summary>
        /// User logged in.
        /// </summary>
        Login,
        
        /// <summary>
        /// User logged out.
        /// </summary>
        Logout,
        
        /// <summary>
        /// Session was locked.
        /// </summary>
        Lock,
        
        /// <summary>
        /// Session was unlocked.
        /// </summary>
        Unlock,
        
        /// <summary>
        /// Session timed out.
        /// </summary>
        Timeout,
        
        /// <summary>
        /// Session was extended.
        /// </summary>
        SessionExtended,
        
        /// <summary>
        /// State synced from another tab.
        /// </summary>
        TabSync,
        
        /// <summary>
        /// Session was restored from persisted storage.
        /// </summary>
        SessionRestored
    }
}