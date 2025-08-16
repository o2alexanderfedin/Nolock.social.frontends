using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoLock.Social.Core.Storage.Interfaces
{
    /// <summary>
    /// Service for managing offline operation queue and synchronization
    /// Provides high-level abstraction for queueing operations when offline
    /// and processing them when connectivity is restored
    /// </summary>
    public interface IOfflineQueueService
    {
        /// <summary>
        /// Queue an operation to be executed when connectivity is restored
        /// </summary>
        /// <param name="operation">The offline operation to queue</param>
        /// <returns>Task that completes when operation is queued</returns>
        Task QueueOperationAsync(OfflineOperation operation);

        /// <summary>
        /// Process all pending operations in the queue
        /// Operations are processed in priority order, then by creation time
        /// </summary>
        /// <returns>Task that completes when all operations are processed</returns>
        Task ProcessQueueAsync();

        /// <summary>
        /// Get the current status of the offline queue
        /// </summary>
        /// <returns>Queue status information including pending operation count</returns>
        Task<OfflineQueueStatus> GetQueueStatusAsync();

        /// <summary>
        /// Clear all processed operations from the queue
        /// Keeps failed operations that may need retry
        /// </summary>
        /// <returns>Task that completes when cleanup is finished</returns>
        Task ClearProcessedOperationsAsync();

        /// <summary>
        /// Event raised when queue processing starts
        /// </summary>
        event EventHandler<OfflineQueueEventArgs>? QueueProcessingStarted;

        /// <summary>
        /// Event raised when queue processing completes
        /// </summary>
        event EventHandler<OfflineQueueEventArgs>? QueueProcessingCompleted;

        /// <summary>
        /// Event raised when an operation succeeds
        /// </summary>
        event EventHandler<OfflineOperationEventArgs>? OperationSucceeded;

        /// <summary>
        /// Event raised when an operation fails
        /// </summary>
        event EventHandler<OfflineOperationEventArgs>? OperationFailed;
    }

    /// <summary>
    /// Status information for the offline operation queue
    /// </summary>
    public class OfflineQueueStatus
    {
        /// <summary>
        /// Total number of pending operations
        /// </summary>
        public int PendingOperations { get; set; }

        /// <summary>
        /// Number of high priority operations (priority 0-1)
        /// </summary>
        public int HighPriorityOperations { get; set; }

        /// <summary>
        /// Number of operations that have failed and are awaiting retry
        /// </summary>
        public int FailedOperations { get; set; }

        /// <summary>
        /// Timestamp of the oldest pending operation
        /// </summary>
        public DateTime? OldestOperationTime { get; set; }

        /// <summary>
        /// Whether the queue is currently being processed
        /// </summary>
        public bool IsProcessing { get; set; }
    }

    /// <summary>
    /// Event arguments for queue-level events
    /// </summary>
    public class OfflineQueueEventArgs : EventArgs
    {
        /// <summary>
        /// Number of operations processed
        /// </summary>
        public int OperationsProcessed { get; set; }

        /// <summary>
        /// Number of operations that succeeded
        /// </summary>
        public int OperationsSucceeded { get; set; }

        /// <summary>
        /// Number of operations that failed
        /// </summary>
        public int OperationsFailed { get; set; }

        /// <summary>
        /// Total processing time
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// Event arguments for operation-level events
    /// </summary>
    public class OfflineOperationEventArgs : EventArgs
    {
        /// <summary>
        /// The operation that was processed
        /// </summary>
        public OfflineOperation Operation { get; set; } = new();

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Exception that caused the failure, if any
        /// </summary>
        public Exception? Exception { get; set; }
    }
}