using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Accessibility.Interfaces;
using NoLock.Social.Core.Common.Constants;

namespace NoLock.Social.Core.Accessibility.Services
{
    /// <summary>
    /// Service for managing screen reader announcements via ARIA live regions
    /// Provides centralized, coordinated announcement handling for consistent accessibility
    /// </summary>
    public class AnnouncementService : IAnnouncementService
    {
        private readonly ILogger<AnnouncementService> _logger;
        private string _currentPoliteAnnouncement = string.Empty;
        private string _currentAssertiveAnnouncement = string.Empty;
        
        public event EventHandler<AnnouncementEventArgs>? OnPoliteAnnouncement;
        public event EventHandler<AnnouncementEventArgs>? OnAssertiveAnnouncement;
        
        public AnnouncementService(ILogger<AnnouncementService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Makes a polite announcement that won't interrupt user activities
        /// </summary>
        public async Task AnnouncePoliteAsync(string message, AnnouncementCategory category = AnnouncementCategory.General)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("Attempted to make polite announcement with empty message");
                return;
            }
            
            try
            {
                _logger.LogDebug("Making polite announcement: {Message} (Category: {Category})", message, category);
                
                _currentPoliteAnnouncement = message;
                
                var eventArgs = new AnnouncementEventArgs
                {
                    Message = message,
                    Category = category,
                    IsAssertive = false,
                    Timestamp = DateTime.UtcNow
                };
                
                OnPoliteAnnouncement?.Invoke(this, eventArgs);
                
                // Clear announcement after a brief delay to allow screen readers to process
                // This enables the same message to be announced again if needed
                await Task.Delay(TimeoutConstants.UI.AnimationDelayMs / 2); // Half animation delay for quick clear
                _currentPoliteAnnouncement = string.Empty;
                
                // Fire event again to clear the live region
                OnPoliteAnnouncement?.Invoke(this, new AnnouncementEventArgs
                {
                    Message = string.Empty,
                    Category = category,
                    IsAssertive = false,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making polite announcement: {Message}", message);
            }
        }
        
        /// <summary>
        /// Makes an assertive announcement that will interrupt user activities
        /// </summary>
        public async Task AnnounceAssertiveAsync(string message, AnnouncementCategory category = AnnouncementCategory.Error)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("Attempted to make assertive announcement with empty message");
                return;
            }
            
            try
            {
                _logger.LogDebug("Making assertive announcement: {Message} (Category: {Category})", message, category);
                
                _currentAssertiveAnnouncement = message;
                
                var eventArgs = new AnnouncementEventArgs
                {
                    Message = message,
                    Category = category,
                    IsAssertive = true,
                    Timestamp = DateTime.UtcNow
                };
                
                OnAssertiveAnnouncement?.Invoke(this, eventArgs);
                
                // For assertive announcements, keep them visible longer
                // as they typically contain important error/warning information
                await Task.Delay(TimeoutConstants.UI.DebounceDelayMs); // Use debounce delay for assertive messages
                _currentAssertiveAnnouncement = string.Empty;
                
                // Fire event again to clear the live region
                OnAssertiveAnnouncement?.Invoke(this, new AnnouncementEventArgs
                {
                    Message = string.Empty,
                    Category = category,
                    IsAssertive = true,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making assertive announcement: {Message}", message);
            }
        }
        
        /// <summary>
        /// Clears all pending announcements
        /// </summary>
        public async Task ClearAnnouncementsAsync()
        {
            try
            {
                _logger.LogDebug("Clearing all announcements");
                
                _currentPoliteAnnouncement = string.Empty;
                _currentAssertiveAnnouncement = string.Empty;
                
                // Fire clear events
                OnPoliteAnnouncement?.Invoke(this, new AnnouncementEventArgs
                {
                    Message = string.Empty,
                    Category = AnnouncementCategory.General,
                    IsAssertive = false,
                    Timestamp = DateTime.UtcNow
                });
                
                OnAssertiveAnnouncement?.Invoke(this, new AnnouncementEventArgs
                {
                    Message = string.Empty,
                    Category = AnnouncementCategory.General,
                    IsAssertive = true,
                    Timestamp = DateTime.UtcNow
                });
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing announcements");
            }
        }
        
        /// <summary>
        /// Gets the current polite announcement message
        /// </summary>
        public string GetCurrentPoliteAnnouncement()
        {
            return _currentPoliteAnnouncement;
        }
        
        /// <summary>
        /// Gets the current assertive announcement message
        /// </summary>
        public string GetCurrentAssertiveAnnouncement()
        {
            return _currentAssertiveAnnouncement;
        }
    }
}