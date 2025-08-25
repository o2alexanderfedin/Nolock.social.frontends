using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Interfaces
{
    /// <summary>
    /// Defines the contract for wake lock service operations that prevent device sleep
    /// during long-running OCR processing operations.
    /// Provides screen wake lock management and page visibility tracking.
    /// </summary>
    public interface IWakeLockService
    {
        /// <summary>
        /// Event raised when the page visibility state changes.
        /// This helps track if the user has switched away from the application.
        /// </summary>
        event EventHandler<VisibilityChangedEventArgs> VisibilityChanged;

        /// <summary>
        /// Event raised when the wake lock status changes (acquired, released, or error).
        /// </summary>
        event EventHandler<WakeLockStatusChangedEventArgs> WakeLockStatusChanged;

        /// <summary>
        /// Gets a value indicating whether wake lock is currently active.
        /// </summary>
        bool IsWakeLockActive { get; }

        /// <summary>
        /// Gets a value indicating whether the page is currently visible to the user.
        /// </summary>
        bool IsPageVisible { get; }

        /// <summary>
        /// Attempts to acquire a screen wake lock to prevent the device from sleeping.
        /// This is essential during OCR processing to maintain processing continuity.
        /// </summary>
        /// <param name="reason">Optional reason for acquiring the wake lock (for debugging/logging).</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains the wake lock acquisition result with success status
        /// and any error information if the operation failed.
        /// </returns>
        Task<WakeLockResult> AcquireWakeLockAsync(string reason = "OCR Processing");

        /// <summary>
        /// Releases the currently active screen wake lock, allowing the device to sleep normally.
        /// Should be called when OCR processing is complete or interrupted.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains the wake lock release result with success status
        /// and any error information if the operation failed.
        /// </returns>
        Task<WakeLockResult> ReleaseWakeLockAsync();

        /// <summary>
        /// Starts monitoring page visibility changes to automatically manage wake lock behavior.
        /// The service will track when the user switches away from the application.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task completes when visibility monitoring is successfully initialized.
        /// </returns>
        Task StartVisibilityMonitoringAsync();

        /// <summary>
        /// Stops monitoring page visibility changes and cleans up event listeners.
        /// Should be called when the service is no longer needed or during disposal.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task completes when visibility monitoring is successfully stopped.
        /// </returns>
        Task StopVisibilityMonitoringAsync();
    }
}