using System;
using System.Collections.Generic;

namespace NoLock.Social.Core.Identity.Storage
{
    /// <summary>
    /// Data stored in localStorage for Remember Me functionality.
    /// IMPORTANT: This class must NEVER contain sensitive data like
    /// passphrases, keys, or session tokens.
    /// </summary>
    public class RememberedUserData
    {
        /// <summary>
        /// The username to remember for convenience.
        /// </summary>
        public string Username { get; set; } = string.Empty;
        
        /// <summary>
        /// When this username was last used for login.
        /// </summary>
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// User preferences that are safe to store (theme, language, etc.).
        /// Never store security-sensitive preferences here.
        /// </summary>
        public Dictionary<string, string> Preferences { get; set; } = new();
    }
}