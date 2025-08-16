using System;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Accessibility.Interfaces
{
    /// <summary>
    /// Service interface for managing screen reader announcements via ARIA live regions
    /// Provides centralized announcement handling for consistent accessibility messaging
    /// </summary>
    public interface IAnnouncementService
    {
        /// <summary>
        /// Event fired when a polite announcement is made (aria-live="polite")
        /// Used for general updates that don't interrupt the user
        /// </summary>
        event EventHandler<AnnouncementEventArgs> OnPoliteAnnouncement;
        
        /// <summary>
        /// Event fired when an assertive announcement is made (aria-live="assertive")
        /// Used for critical errors/warnings that should interrupt the user
        /// </summary>
        event EventHandler<AnnouncementEventArgs> OnAssertiveAnnouncement;
        
        /// <summary>
        /// Makes a polite announcement that won't interrupt user activities
        /// Suitable for status updates, confirmations, and general information
        /// </summary>
        /// <param name="message">The message to announce</param>
        /// <param name="category">Optional category for filtering/logging</param>
        /// <returns>Task representing the async operation</returns>
        Task AnnouncePoliteAsync(string message, AnnouncementCategory category = AnnouncementCategory.General);
        
        /// <summary>
        /// Makes an assertive announcement that will interrupt user activities
        /// Suitable for errors, warnings, and urgent information
        /// </summary>
        /// <param name="message">The message to announce</param>
        /// <param name="category">Optional category for filtering/logging</param>
        /// <returns>Task representing the async operation</returns>
        Task AnnounceAssertiveAsync(string message, AnnouncementCategory category = AnnouncementCategory.Error);
        
        /// <summary>
        /// Clears all pending announcements
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        Task ClearAnnouncementsAsync();
        
        /// <summary>
        /// Gets the current polite announcement message
        /// </summary>
        /// <returns>Current polite announcement or empty string</returns>
        string GetCurrentPoliteAnnouncement();
        
        /// <summary>
        /// Gets the current assertive announcement message
        /// </summary>
        /// <returns>Current assertive announcement or empty string</returns>
        string GetCurrentAssertiveAnnouncement();
    }
    
    /// <summary>
    /// Categories for organizing different types of announcements
    /// </summary>
    public enum AnnouncementCategory
    {
        General,
        CameraOperation,
        PageManagement,
        QualityFeedback,
        Navigation,
        Error,
        OfflineStatus,
        VoiceCommand
    }
    
    /// <summary>
    /// Event arguments for announcement events
    /// </summary>
    public class AnnouncementEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public AnnouncementCategory Category { get; set; } = AnnouncementCategory.General;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsAssertive { get; set; } = false;
    }
}