using System;

namespace NoLock.Social.Core.Cryptography.Interfaces
{
    /// <summary>
    /// Represents a cryptographic identity session
    /// </summary>
    public class IdentitySession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = string.Empty;
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
        public bool IsLocked { get; set; } = true;
        
        /// <summary>
        /// Private key is stored in SecureBuffer, not directly in the session
        /// </summary>
        public ISecureBuffer? PrivateKeyBuffer { get; set; }
    }

    /// <summary>
    /// Session state change event arguments
    /// </summary>
    public class SessionStateChangedEventArgs : EventArgs
    {
        public SessionState OldState { get; set; }
        public SessionState NewState { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Session state enumeration
    /// </summary>
    public enum SessionState
    {
        Locked,
        Unlocking,
        Unlocked,
        Locking,
        Expired
    }
}