using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Interfaces
{
    /// <summary>
    /// Defines the contract for background processing queue management.
    /// Provides document queuing, processing control, and state persistence for OCR operations.
    /// </summary>
    public interface IBackgroundProcessingQueue
    {
        /// <summary>
        /// Event raised when a document is added to the queue.
        /// </summary>
        event EventHandler<QueuedDocumentEventArgs> DocumentQueued;

        /// <summary>
        /// Event raised when document processing status changes.
        /// </summary>
        event EventHandler<QueuedDocumentEventArgs> ProcessingStatusChanged;

        /// <summary>
        /// Event raised when a document processing completes (success or failure).
        /// </summary>
        event EventHandler<QueuedDocumentEventArgs> ProcessingCompleted;

        /// <summary>
        /// Event raised when queue state changes (paused, resumed, etc.).
        /// </summary>
        event EventHandler<QueueStateChangedEventArgs> QueueStateChanged;

        /// <summary>
        /// Gets the current state of the processing queue.
        /// </summary>
        QueueState CurrentState { get; }

        /// <summary>
        /// Gets the total number of documents in the queue.
        /// </summary>
        int QueueCount { get; }

        /// <summary>
        /// Gets the number of documents currently being processed.
        /// </summary>
        int ProcessingCount { get; }

        /// <summary>
        /// Gets the number of completed documents (success and failed).
        /// </summary>
        int CompletedCount { get; }

        /// <summary>
        /// Adds a document to the processing queue for background OCR processing.
        /// The document will be persisted to IndexedDB for cross-session persistence.
        /// </summary>
        /// <param name="request">The OCR submission request for the document.</param>
        /// <param name="priority">Priority level for queue ordering (higher values processed first).</param>
        /// <param name="metadata">Additional metadata associated with the queued document.</param>
        /// <param name="cancellation">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous enqueue operation.
        /// The task result contains the unique queue ID for tracking.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when queue is shut down.</exception>
        Task<string> EnqueueDocumentAsync(
            OCRSubmissionRequest request,
            QueuePriority priority = QueuePriority.Normal,
            Dictionary<string, object>? metadata = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Gets all documents currently in the queue with their status information.
        /// </summary>
        /// <param name="cancellation">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains a list of all queued documents.
        /// </returns>
        Task<IReadOnlyList<QueuedDocument>> GetQueuedDocumentsAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Gets a specific queued document by its queue ID.
        /// </summary>
        /// <param name="queueId">The unique queue identifier for the document.</param>
        /// <param name="cancellation">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains the queued document or null if not found.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when queueId is null or empty.</exception>
        Task<QueuedDocument> GetQueuedDocumentAsync(string queueId, CancellationToken cancellation = default);

        /// <summary>
        /// Removes a document from the queue by its queue ID.
        /// If the document is currently processing, it will be cancelled.
        /// </summary>
        /// <param name="queueId">The unique queue identifier for the document to remove.</param>
        /// <param name="cancellation">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result indicates whether the document was successfully removed.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when queueId is null or empty.</exception>
        Task<bool> RemoveDocumentAsync(string queueId, CancellationToken cancellation = default);

        /// <summary>
        /// Pauses the processing queue. Documents already being processed will continue,
        /// but no new documents will be started until the queue is resumed.
        /// </summary>
        /// <param name="cancellation">Cancellation token for the async operation.</param>
        /// <returns>A task that represents the asynchronous pause operation.</returns>
        Task PauseProcessingAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Resumes the processing queue, allowing queued documents to be processed.
        /// </summary>
        /// <param name="cancellation">Cancellation token for the async operation.</param>
        /// <returns>A task that represents the asynchronous resume operation.</returns>
        Task ResumeProcessingAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Cancels processing of a specific document if it's currently being processed.
        /// The document will be marked as cancelled and removed from active processing.
        /// </summary>
        /// <param name="queueId">The unique queue identifier for the document to cancel.</param>
        /// <param name="cancellation">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous cancellation operation.
        /// The task result indicates whether the cancellation was successful.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when queueId is null or empty.</exception>
        Task<bool> CancelDocumentProcessingAsync(string queueId, CancellationToken cancellation = default);

        /// <summary>
        /// Clears all completed documents from the queue (both successful and failed).
        /// Documents currently queued or processing are not affected.
        /// </summary>
        /// <param name="cancellation">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains the number of documents cleared.
        /// </returns>
        Task<int> ClearCompletedDocumentsAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Retries processing of a failed document by moving it back to the queue.
        /// </summary>
        /// <param name="queueId">The unique queue identifier for the document to retry.</param>
        /// <param name="cancellation">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous retry operation.
        /// The task result indicates whether the retry was successful.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when queueId is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when document is not in a failed state.</exception>
        Task<bool> RetryDocumentAsync(string queueId, CancellationToken cancellation = default);

        /// <summary>
        /// Starts the background processing service. This method should be called once
        /// during application startup to begin processing queued documents.
        /// </summary>
        /// <param name="cancellation">Cancellation token for the background service.</param>
        /// <returns>A task that represents the asynchronous startup operation.</returns>
        Task StartAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Stops the background processing service gracefully. Current processing
        /// operations will be allowed to complete, but no new processing will start.
        /// </summary>
        /// <param name="cancellation">Cancellation token for the shutdown operation.</param>
        /// <returns>A task that represents the asynchronous shutdown operation.</returns>
        Task StopAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Gets queue processing statistics including throughput and performance metrics.
        /// </summary>
        /// <param name="cancellation">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains queue processing statistics.
        /// </returns>
        Task<QueueStatistics> GetStatisticsAsync(CancellationToken cancellation = default);
    }

    /// <summary>
    /// Event arguments for queued document events.
    /// </summary>
    public class QueuedDocumentEventArgs : EventArgs
    {
        /// <summary>
        /// The queued document associated with the event.
        /// </summary>
        public QueuedDocument Document { get; }

        /// <summary>
        /// Initializes a new instance of the QueuedDocumentEventArgs class.
        /// </summary>
        /// <param name="document">The queued document.</param>
        public QueuedDocumentEventArgs(QueuedDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }
    }

    /// <summary>
    /// Event arguments for queue state change events.
    /// </summary>
    public class QueueStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The previous state of the queue.
        /// </summary>
        public QueueState PreviousState { get; }

        /// <summary>
        /// The current state of the queue.
        /// </summary>
        public QueueState CurrentState { get; }

        /// <summary>
        /// Timestamp when the state change occurred.
        /// </summary>
        public DateTime ChangedAt { get; }

        /// <summary>
        /// Initializes a new instance of the QueueStateChangedEventArgs class.
        /// </summary>
        /// <param name="previousState">The previous queue state.</param>
        /// <param name="currentState">The current queue state.</param>
        public QueueStateChangedEventArgs(QueueState previousState, QueueState currentState)
        {
            PreviousState = previousState;
            CurrentState = currentState;
            ChangedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Enumeration of queue processing states.
    /// </summary>
    public enum QueueState
    {
        /// <summary>
        /// Queue is stopped and not processing documents.
        /// </summary>
        Stopped = 0,

        /// <summary>
        /// Queue is running and processing documents.
        /// </summary>
        Running = 1,

        /// <summary>
        /// Queue is paused and not starting new document processing.
        /// </summary>
        Paused = 2,

        /// <summary>
        /// Queue is shutting down gracefully.
        /// </summary>
        Stopping = 3
    }

    /// <summary>
    /// Enumeration of document queue priorities.
    /// </summary>
    public enum QueuePriority
    {
        /// <summary>
        /// Low priority - processed after normal and high priority documents.
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal priority - default processing priority.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority - processed before normal and low priority documents.
        /// </summary>
        High = 2,

        /// <summary>
        /// Critical priority - processed immediately before all other documents.
        /// </summary>
        Critical = 3
    }

    /// <summary>
    /// Queue processing statistics and performance metrics.
    /// </summary>
    public class QueueStatistics
    {
        /// <summary>
        /// Total number of documents processed since startup.
        /// </summary>
        public int TotalProcessed { get; set; }

        /// <summary>
        /// Number of successfully processed documents.
        /// </summary>
        public int SuccessfullyProcessed { get; set; }

        /// <summary>
        /// Number of failed document processing attempts.
        /// </summary>
        public int FailedProcessing { get; set; }

        /// <summary>
        /// Average processing time per document in milliseconds.
        /// </summary>
        public double AverageProcessingTimeMs { get; set; }

        /// <summary>
        /// Current queue throughput in documents per minute.
        /// </summary>
        public double ThroughputPerMinute { get; set; }

        /// <summary>
        /// Time when statistics were last calculated.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Time when the queue service was started.
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Total uptime of the queue service.
        /// </summary>
        public TimeSpan Uptime => DateTime.UtcNow - StartedAt;

        /// <summary>
        /// Success rate as a percentage (0-100).
        /// </summary>
        public double SuccessRate => TotalProcessed > 0 
            ? (SuccessfullyProcessed / (double)TotalProcessed) * 100 
            : 0;
    }
}