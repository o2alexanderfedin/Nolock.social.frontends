using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NoLock.Social.Core.Camera.Models;

namespace NoLock.Social.Core.Storage.Interfaces
{
    /// <summary>
    /// Service for offline storage using IndexedDB for Blazor WebAssembly
    /// Handles persistent storage of document sessions and captured images
    /// </summary>
    public interface IOfflineStorageService : IDisposable
    {
        /// <summary>
        /// Save a document session to offline storage
        /// </summary>
        /// <param name="session">The document session to store</param>
        /// <returns>Task that completes when session is saved</returns>
        Task SaveSessionAsync(DocumentSession session);

        /// <summary>
        /// Load a document session from offline storage
        /// </summary>
        /// <param name="sessionId">The unique session identifier</param>
        /// <returns>The document session if found, null otherwise</returns>
        Task<DocumentSession?> LoadSessionAsync(string sessionId);

        /// <summary>
        /// Save a captured image to offline storage
        /// </summary>
        /// <param name="image">The captured image to store</param>
        /// <returns>Task that completes when image is saved</returns>
        Task SaveImageAsync(CapturedImage image);

        /// <summary>
        /// Load a captured image from offline storage
        /// </summary>
        /// <param name="imageId">The unique image identifier (timestamp-based)</param>
        /// <returns>The captured image if found, null otherwise</returns>
        Task<CapturedImage?> LoadImageAsync(string imageId);

        /// <summary>
        /// Queue an offline operation for later execution
        /// </summary>
        /// <param name="operation">The offline operation to queue</param>
        /// <returns>Task that completes when operation is queued</returns>
        Task QueueOfflineOperationAsync(OfflineOperation operation);

        /// <summary>
        /// Get all pending offline operations
        /// </summary>
        /// <returns>List of pending operations</returns>
        Task<List<OfflineOperation>> GetPendingOperationsAsync();

        /// <summary>
        /// Remove a completed offline operation from the queue
        /// </summary>
        /// <param name="operationId">The operation identifier</param>
        /// <returns>Task that completes when operation is removed</returns>
        Task RemoveOperationAsync(string operationId);

        /// <summary>
        /// Clear all offline data (sessions, images, operations)
        /// </summary>
        /// <returns>Task that completes when all data is cleared</returns>
        Task ClearAllDataAsync();

        /// <summary>
        /// Get all stored document sessions
        /// </summary>
        /// <returns>List of all document sessions</returns>
        Task<List<DocumentSession>> GetAllSessionsAsync();
    }

    /// <summary>
    /// Represents an offline operation that needs to be executed when online
    /// </summary>
    public class OfflineOperation
    {
        /// <summary>
        /// Unique identifier for the operation
        /// </summary>
        public string OperationId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Type of operation (e.g., "upload", "sync", "delete")
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// JSON payload containing operation data
        /// </summary>
        public string Payload { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when operation was queued
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of retry attempts
        /// </summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// Maximum number of retry attempts
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Priority level (lower number = higher priority)
        /// </summary>
        public int Priority { get; set; } = 0;
    }

    /// <summary>
    /// Exception thrown when offline storage operations fail
    /// </summary>
    public class OfflineStorageException : Exception
    {
        public string Operation { get; }

        public OfflineStorageException(string message, string operation) 
            : base(message)
        {
            Operation = operation;
        }

        public OfflineStorageException(string message, string operation, Exception innerException) 
            : base(message, innerException)
        {
            Operation = operation;
        }
    }
}