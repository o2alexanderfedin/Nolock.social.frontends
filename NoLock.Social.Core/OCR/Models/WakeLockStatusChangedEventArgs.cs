namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Event arguments for wake lock status change events.
    /// Provides information about wake lock state transitions and any associated errors.
    /// </summary>
    public class WakeLockStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Indicates whether the wake lock is currently active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// The previous wake lock active state before this change.
        /// </summary>
        public bool PreviousState { get; set; }

        /// <summary>
        /// The type of operation that caused this status change.
        /// </summary>
        public WakeLockOperationType OperationType { get; set; }

        /// <summary>
        /// The result of the wake lock operation that triggered this event.
        /// Contains success status and any error information.
        /// </summary>
        public WakeLockResult OperationResult { get; set; }

        /// <summary>
        /// Timestamp when the wake lock status change occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Optional reason provided when the wake lock operation was initiated.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Creates a new instance of WakeLockStatusChangedEventArgs.
        /// </summary>
        /// <param name="isActive">Whether the wake lock is currently active.</param>
        /// <param name="previousState">The previous wake lock state.</param>
        /// <param name="operationType">The type of operation that caused this change.</param>
        /// <param name="operationResult">The result of the wake lock operation.</param>
        /// <param name="reason">Optional reason for the operation.</param>
        public WakeLockStatusChangedEventArgs(
            bool isActive,
            bool previousState,
            WakeLockOperationType operationType,
            WakeLockResult operationResult,
            string? reason = null)
        {
            IsActive = isActive;
            PreviousState = previousState;
            OperationType = operationType;
            OperationResult = operationResult;
            Reason = reason;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets a value indicating whether the wake lock was successfully acquired.
        /// </summary>
        public bool IsAcquired => IsActive && !PreviousState && OperationType == WakeLockOperationType.Acquire;

        /// <summary>
        /// Gets a value indicating whether the wake lock was successfully released.
        /// </summary>
        public bool IsReleased => !IsActive && PreviousState && OperationType == WakeLockOperationType.Release;

        /// <summary>
        /// Gets a value indicating whether the operation failed.
        /// </summary>
        public bool HasError => !OperationResult.IsSuccess;
    }
}