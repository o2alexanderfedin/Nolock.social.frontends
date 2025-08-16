using System;

namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Event arguments for page visibility change events.
    /// Provides information about the current visibility state and timing.
    /// </summary>
    public class VisibilityChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Indicates whether the page is currently visible to the user.
        /// </summary>
        public bool IsVisible { get; set; }

        /// <summary>
        /// The previous visibility state before this change.
        /// </summary>
        public bool PreviousState { get; set; }

        /// <summary>
        /// Timestamp when the visibility change occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// The visibility state as reported by the browser's Page Visibility API.
        /// Common values: "visible", "hidden", "prerender", "unloaded"
        /// </summary>
        public string? VisibilityState { get; set; }

        /// <summary>
        /// Creates a new instance of VisibilityChangedEventArgs.
        /// </summary>
        /// <param name="isVisible">Whether the page is currently visible.</param>
        /// <param name="previousState">The previous visibility state.</param>
        /// <param name="visibilityState">The browser's visibility state string.</param>
        public VisibilityChangedEventArgs(bool isVisible, bool previousState, string? visibilityState = null)
        {
            IsVisible = isVisible;
            PreviousState = previousState;
            VisibilityState = visibilityState;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets a value indicating whether this represents a transition from hidden to visible.
        /// </summary>
        public bool IsBecomeVisible => !PreviousState && IsVisible;

        /// <summary>
        /// Gets a value indicating whether this represents a transition from visible to hidden.
        /// </summary>
        public bool IsBecomeHidden => PreviousState && !IsVisible;
    }
}