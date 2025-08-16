using System;
using System.Threading.Tasks;
using NoLock.Social.Core.Camera.Models;

namespace NoLock.Social.Core.Storage.Interfaces
{
    /// <summary>
    /// Service for synchronizing offline data with the server when connectivity is restored
    /// Processes queued offline operations and handles sync conflicts and error scenarios
    /// </summary>
    public interface ISyncService
    {
        /// <summary>
        /// Synchronize all offline data with the server
        /// Processes all queued operations in priority order
        /// </summary>
        /// <returns>Task that completes when sync operation finishes</returns>
        Task SyncOfflineDataAsync();

        /// <summary>
        /// Synchronize a specific document session with the server
        /// </summary>
        /// <param name="session">The document session to sync</param>
        /// <returns>Task that completes when session sync finishes</returns>
        Task SyncSessionAsync(DocumentSession session);

        /// <summary>
        /// Get the current synchronization status
        /// </summary>
        /// <returns>Sync status information including progress and pending operations</returns>
        Task<SyncStatus> GetSyncStatusAsync();

        /// <summary>
        /// Event raised when sync operation starts
        /// </summary>
        event EventHandler<SyncStartedEventArgs>? SyncStarted;

        /// <summary>
        /// Event raised when sync operation progresses
        /// </summary>
        event EventHandler<SyncProgressEventArgs>? SyncProgress;

        /// <summary>
        /// Event raised when sync operation completes
        /// </summary>
        event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

        /// <summary>
        /// Event raised when a sync conflict occurs
        /// </summary>
        event EventHandler<SyncConflictEventArgs>? SyncConflict;
    }

    /// <summary>
    /// Current status of the synchronization process
    /// </summary>
    public class SyncStatus
    {
        /// <summary>
        /// Whether a sync operation is currently in progress
        /// </summary>
        public bool IsSyncing { get; set; }

        /// <summary>
        /// Total number of operations to sync
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Number of operations completed
        /// </summary>
        public int CompletedOperations { get; set; }

        /// <summary>
        /// Number of operations that failed
        /// </summary>
        public int FailedOperations { get; set; }

        /// <summary>
        /// Current operation being processed
        /// </summary>
        public string? CurrentOperation { get; set; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double ProgressPercentage => TotalOperations > 0 
            ? (double)CompletedOperations / TotalOperations * 100 
            : 0;

        /// <summary>
        /// Time when sync started
        /// </summary>
        public DateTime? SyncStartTime { get; set; }

        /// <summary>
        /// Estimated time remaining
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// Event arguments for sync started event
    /// </summary>
    public class SyncStartedEventArgs : EventArgs
    {
        /// <summary>
        /// Total number of operations to sync
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Types of operations being synced
        /// </summary>
        public string[] OperationTypes { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Event arguments for sync progress event
    /// </summary>
    public class SyncProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Number of operations completed
        /// </summary>
        public int CompletedOperations { get; set; }

        /// <summary>
        /// Total number of operations
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Current operation being processed
        /// </summary>
        public string CurrentOperation { get; set; } = string.Empty;

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double ProgressPercentage { get; set; }
    }

    /// <summary>
    /// Event arguments for sync completed event
    /// </summary>
    public class SyncCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Whether the sync completed successfully
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Total number of operations processed
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Number of operations that succeeded
        /// </summary>
        public int SuccessfulOperations { get; set; }

        /// <summary>
        /// Number of operations that failed
        /// </summary>
        public int FailedOperations { get; set; }

        /// <summary>
        /// Total time taken for sync
        /// </summary>
        public TimeSpan SyncDuration { get; set; }

        /// <summary>
        /// Error message if sync failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Event arguments for sync conflict event
    /// </summary>
    public class SyncConflictEventArgs : EventArgs
    {
        /// <summary>
        /// The operation that caused the conflict
        /// </summary>
        public OfflineOperation Operation { get; set; } = new();

        /// <summary>
        /// Description of the conflict
        /// </summary>
        public string ConflictDescription { get; set; } = string.Empty;

        /// <summary>
        /// Suggested resolution strategy
        /// </summary>
        public ConflictResolutionStrategy SuggestedResolution { get; set; }

        /// <summary>
        /// Whether the conflict was resolved automatically
        /// </summary>
        public bool AutoResolved { get; set; }
    }

    /// <summary>
    /// Strategies for resolving sync conflicts
    /// </summary>
    public enum ConflictResolutionStrategy
    {
        /// <summary>
        /// Keep the local version
        /// </summary>
        KeepLocal,

        /// <summary>
        /// Use the server version
        /// </summary>
        UseServer,

        /// <summary>
        /// Merge both versions
        /// </summary>
        Merge,

        /// <summary>
        /// Skip this operation
        /// </summary>
        Skip,

        /// <summary>
        /// Retry the operation
        /// </summary>
        Retry
    }
}