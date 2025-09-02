using NoLock.Social.Core.OCR.Interfaces;

namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents a document queued for background OCR processing.
    /// Contains all necessary metadata for tracking, processing, and persistence.
    /// </summary>
    public class QueuedDocument
    {
        /// <summary>
        /// Unique identifier for this queued document entry.
        /// Used for tracking and queue operations.
        /// </summary>
        public string QueueId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The original OCR submission request for this document.
        /// Contains image data, document type, and client metadata.
        /// </summary>
        public OCRSubmissionRequest SubmissionRequest { get; set; }

        /// <summary>
        /// Current processing status of the queued document.
        /// </summary>
        public QueuedDocumentStatus Status { get; set; } = QueuedDocumentStatus.Queued;

        /// <summary>
        /// Priority level for queue ordering (higher values processed first).
        /// </summary>
        public QueuePriority Priority { get; set; } = QueuePriority.Normal;

        /// <summary>
        /// Timestamp when the document was added to the queue.
        /// </summary>
        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when processing started for this document.
        /// Null if processing hasn't started yet.
        /// </summary>
        public DateTime? ProcessingStartedAt { get; set; }

        /// <summary>
        /// Timestamp when processing completed (success or failure).
        /// Null if processing hasn't completed yet.
        /// </summary>
        public DateTime? ProcessingCompletedAt { get; set; }

        /// <summary>
        /// OCR service tracking ID assigned when submitted to the OCR service.
        /// Used for polling OCR status. Null if not yet submitted.
        /// </summary>
        public string OcrTrackingId { get; set; }

        /// <summary>
        /// Current OCR processing status from the OCR service.
        /// Updated during polling operations.
        /// </summary>
        public OCRProcessingStatus? OcrStatus { get; set; }

        /// <summary>
        /// OCR processing progress percentage (0-100).
        /// Updated during polling operations.
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Final OCR processing result when completed successfully.
        /// Null if processing is not yet complete or failed.
        /// </summary>
        public OCRStatusResponse ProcessingResult { get; set; }

        /// <summary>
        /// Error message if processing failed at any stage.
        /// Null if no error occurred.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Error code categorizing the type of error that occurred.
        /// Null if no error occurred.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Exception details if an unexpected error occurred during processing.
        /// Stored for debugging purposes. Null if no exception occurred.
        /// </summary>
        public string ExceptionDetails { get; set; }

        /// <summary>
        /// Number of retry attempts made for this document.
        /// Incremented each time processing is retried after failure.
        /// </summary>
        public int RetryAttempts { get; set; }

        /// <summary>
        /// Maximum number of retry attempts allowed for this document.
        /// Default is 3 attempts.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Timestamp of the last retry attempt.
        /// Null if no retries have been attempted.
        /// </summary>
        public DateTime? LastRetryAt { get; set; }

        /// <summary>
        /// Cancellation token source for controlling document processing.
        /// Used to cancel processing operations. Null if no cancellation capability.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public CancellationTokenSource CancellationTokenSource { get; set; }

        /// <summary>
        /// Additional metadata associated with the queued document.
        /// Can store custom application-specific data.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Estimated completion time based on queue position and processing history.
        /// Updated dynamically during queue processing.
        /// </summary>
        public DateTime? EstimatedCompletionTime { get; set; }

        /// <summary>
        /// Current position in the processing queue.
        /// Updated as documents are processed and queue order changes.
        /// </summary>
        public int QueuePosition { get; set; }

        /// <summary>
        /// Total processing time for this document in milliseconds.
        /// Calculated when processing completes.
        /// </summary>
        public long? ProcessingTimeMs { get; set; }

        /// <summary>
        /// Source that originated this queue request (UI component, API, batch import, etc.).
        /// Used for tracking and analytics.
        /// </summary>
        public string SourceIdentifier { get; set; }

        /// <summary>
        /// Version of the queue document schema for migration compatibility.
        /// </summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>
        /// Indicates whether this document can be cancelled.
        /// Documents can be cancelled when queued or processing.
        /// </summary>
        public bool IsCancellable => Status == QueuedDocumentStatus.Queued || 
                                   Status == QueuedDocumentStatus.Processing;

        /// <summary>
        /// Indicates whether this document can be retried.
        /// Documents can be retried when failed and under retry limit.
        /// </summary>
        public bool IsRetryable => Status == QueuedDocumentStatus.Failed && 
                                 RetryAttempts < MaxRetryAttempts;

        /// <summary>
        /// Indicates whether processing is complete (success or permanent failure).
        /// </summary>
        public bool IsCompleted => Status == QueuedDocumentStatus.Completed || 
                                 Status == QueuedDocumentStatus.Failed ||
                                 Status == QueuedDocumentStatus.Cancelled;

        /// <summary>
        /// Gets the total elapsed time since the document was queued.
        /// </summary>
        public TimeSpan TotalElapsedTime => DateTime.UtcNow - QueuedAt;

        /// <summary>
        /// Gets the processing time if completed, otherwise current processing duration.
        /// </summary>
        public TimeSpan? ProcessingDuration
        {
            get
            {
                if (ProcessingTimeMs.HasValue)
                    return TimeSpan.FromMilliseconds(ProcessingTimeMs.Value);
                
                if (ProcessingStartedAt.HasValue)
                {
                    var endTime = ProcessingCompletedAt ?? DateTime.UtcNow;
                    return endTime - ProcessingStartedAt.Value;
                }
                
                return null;
            }
        }

        /// <summary>
        /// Creates a new instance of QueuedDocument from an OCR submission request.
        /// </summary>
        /// <param name="request">The OCR submission request.</param>
        /// <param name="priority">Priority level for queue ordering.</param>
        /// <param name="metadata">Additional metadata for the queued document.</param>
        /// <param name="sourceIdentifier">Source that originated this request.</param>
        /// <returns>A new QueuedDocument instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
        public static QueuedDocument CreateFromRequest(
            OCRSubmissionRequest request,
            QueuePriority priority = QueuePriority.Normal,
            Dictionary<string, object> metadata = null,
            string sourceIdentifier = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return new QueuedDocument
            {
                SubmissionRequest = request,
                Priority = priority,
                Metadata = metadata ?? new Dictionary<string, object>(),
                SourceIdentifier = sourceIdentifier ?? "Unknown",
                CancellationTokenSource = new CancellationTokenSource()
            };
        }

        /// <summary>
        /// Updates the document status and related timestamps.
        /// </summary>
        /// <param name="newStatus">The new status to set.</param>
        /// <param name="errorMessage">Optional error message if status indicates failure.</param>
        /// <param name="errorCode">Optional error code for categorizing the error.</param>
        public void UpdateStatus(QueuedDocumentStatus newStatus, string errorMessage = null, string errorCode = null)
        {
            var previousStatus = Status;
            Status = newStatus;

            // Update timestamps based on status transitions
            switch (newStatus)
            {
                case QueuedDocumentStatus.Processing when previousStatus == QueuedDocumentStatus.Queued:
                    ProcessingStartedAt = DateTime.UtcNow;
                    break;

                case QueuedDocumentStatus.Completed:
                case QueuedDocumentStatus.Failed:
                case QueuedDocumentStatus.Cancelled:
                    if (!ProcessingCompletedAt.HasValue)
                    {
                        ProcessingCompletedAt = DateTime.UtcNow;
                        if (ProcessingStartedAt.HasValue)
                        {
                            ProcessingTimeMs = (long)(ProcessingCompletedAt.Value - ProcessingStartedAt.Value).TotalMilliseconds;
                        }
                    }
                    break;
            }

            // Set error information for failed status
            if (newStatus == QueuedDocumentStatus.Failed)
            {
                ErrorMessage = errorMessage;
                ErrorCode = errorCode;
            }
        }

        /// <summary>
        /// Updates OCR processing information from a status response.
        /// </summary>
        /// <param name="statusResponse">The OCR status response.</param>
        public void UpdateFromOcrStatus(OCRStatusResponse statusResponse)
        {
            if (statusResponse == null) return;

            OcrStatus = statusResponse.Status;
            ProgressPercentage = statusResponse.ProgressPercentage;
            
            // Always store the OCR status response
            ProcessingResult = statusResponse;

            // Map OCR status to queue status if appropriate
            switch (statusResponse.Status)
            {
                case OCRProcessingStatus.Complete:
                    UpdateStatus(QueuedDocumentStatus.Completed);
                    break;

                case OCRProcessingStatus.Failed:
                    UpdateStatus(QueuedDocumentStatus.Failed, 
                               statusResponse.ErrorMessage, 
                               statusResponse.ErrorCode);
                    break;
            }
        }

        /// <summary>
        /// Prepares the document for retry by resetting processing state.
        /// </summary>
        public void PrepareForRetry()
        {
            if (!IsRetryable)
                throw new InvalidOperationException("Document is not in a retryable state.");

            RetryAttempts++;
            LastRetryAt = DateTime.UtcNow;
            
            // Reset processing state
            Status = QueuedDocumentStatus.Queued;
            ProcessingStartedAt = null;
            ProcessingCompletedAt = null;
            ProcessingTimeMs = null;
            OcrTrackingId = null;
            OcrStatus = null;
            ProgressPercentage = 0;
            ProcessingResult = null;
            
            // Clear previous error state but keep history
            ErrorMessage = null;
            ErrorCode = null;
            ExceptionDetails = null;

            // Create new cancellation token
            CancellationTokenSource?.Dispose();
            CancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Cancels the document processing if cancellable.
        /// </summary>
        public void Cancel()
        {
            if (!IsCancellable)
                throw new InvalidOperationException("Document is not in a cancellable state.");

            UpdateStatus(QueuedDocumentStatus.Cancelled);
            CancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Creates a deep copy of this queued document for persistence or transfer.
        /// Excludes non-serializable components like CancellationTokenSource.
        /// </summary>
        /// <returns>A new QueuedDocument instance with copied data.</returns>
        public QueuedDocument CreatePersistableCopy()
        {
            return new QueuedDocument
            {
                QueueId = QueueId,
                SubmissionRequest = SubmissionRequest,
                Status = Status,
                Priority = Priority,
                QueuedAt = QueuedAt,
                ProcessingStartedAt = ProcessingStartedAt,
                ProcessingCompletedAt = ProcessingCompletedAt,
                OcrTrackingId = OcrTrackingId,
                OcrStatus = OcrStatus,
                ProgressPercentage = ProgressPercentage,
                ProcessingResult = ProcessingResult,
                ErrorMessage = ErrorMessage,
                ErrorCode = ErrorCode,
                ExceptionDetails = ExceptionDetails,
                RetryAttempts = RetryAttempts,
                MaxRetryAttempts = MaxRetryAttempts,
                LastRetryAt = LastRetryAt,
                Metadata = new Dictionary<string, object>(Metadata),
                EstimatedCompletionTime = EstimatedCompletionTime,
                QueuePosition = QueuePosition,
                ProcessingTimeMs = ProcessingTimeMs,
                SourceIdentifier = SourceIdentifier,
                SchemaVersion = SchemaVersion,
                // CancellationTokenSource is not copied as it's not serializable
                CancellationTokenSource = null
            };
        }
    }

    /// <summary>
    /// Enumeration of queued document processing states.
    /// Tracks the lifecycle of a document through the background processing queue.
    /// </summary>
    public enum QueuedDocumentStatus
    {
        /// <summary>
        /// Document is queued and waiting to be processed.
        /// </summary>
        Queued = 0,

        /// <summary>
        /// Document is currently being processed by the OCR service.
        /// </summary>
        Processing = 1,

        /// <summary>
        /// Document processing completed successfully.
        /// </summary>
        Completed = 2,

        /// <summary>
        /// Document processing failed (permanently or until retry).
        /// </summary>
        Failed = 3,

        /// <summary>
        /// Document processing was cancelled by user or system.
        /// </summary>
        Cancelled = 4,

        /// <summary>
        /// Document is scheduled for retry after a previous failure.
        /// </summary>
        Retrying = 5
    }
}