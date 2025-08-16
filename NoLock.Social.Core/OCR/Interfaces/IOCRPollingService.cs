using System;
using System.Threading;
using System.Threading.Tasks;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Interfaces
{
    /// <summary>
    /// Defines the contract for polling OCR processing status with automatic result retrieval.
    /// Provides high-level polling operations specifically for OCR status checking.
    /// </summary>
    public interface IOCRPollingService
    {
        /// <summary>
        /// Polls the OCR service for processing status until completion or timeout.
        /// Automatically retrieves results when processing is complete.
        /// </summary>
        /// <param name="trackingId">The unique tracking identifier for the OCR submission.</param>
        /// <param name="cancellationToken">Cancellation token for the polling operation.</param>
        /// <returns>
        /// A task that represents the asynchronous polling operation.
        /// The task result contains the final OCR status with complete results.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when trackingId is null or empty.</exception>
        /// <exception cref="TimeoutException">Thrown when polling exceeds maximum duration.</exception>
        /// <exception cref="OCRProcessingException">Thrown when OCR processing fails.</exception>
        /// <exception cref="OperationCanceledException">Thrown when polling is cancelled.</exception>
        Task<OCRStatusResponse> PollForCompletionAsync(
            string trackingId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Polls the OCR service with progress updates until completion or timeout.
        /// Provides real-time status updates through the progress callback.
        /// </summary>
        /// <param name="trackingId">The unique tracking identifier for the OCR submission.</param>
        /// <param name="progressCallback">Callback invoked with status updates during polling.</param>
        /// <param name="cancellationToken">Cancellation token for the polling operation.</param>
        /// <returns>
        /// A task that represents the asynchronous polling operation.
        /// The task result contains the final OCR status with complete results.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when trackingId is null or empty.</exception>
        /// <exception cref="TimeoutException">Thrown when polling exceeds maximum duration.</exception>
        /// <exception cref="OCRProcessingException">Thrown when OCR processing fails.</exception>
        /// <exception cref="OperationCanceledException">Thrown when polling is cancelled.</exception>
        Task<OCRStatusResponse> PollForCompletionWithProgressAsync(
            string trackingId,
            Action<OCRStatusUpdate> progressCallback,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Polls the OCR service with custom configuration until completion or timeout.
        /// Allows override of default polling intervals and timeout settings.
        /// </summary>
        /// <param name="trackingId">The unique tracking identifier for the OCR submission.</param>
        /// <param name="configuration">Custom polling configuration to use.</param>
        /// <param name="cancellationToken">Cancellation token for the polling operation.</param>
        /// <returns>
        /// A task that represents the asynchronous polling operation.
        /// The task result contains the final OCR status with complete results.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        /// <exception cref="TimeoutException">Thrown when polling exceeds maximum duration.</exception>
        /// <exception cref="OCRProcessingException">Thrown when OCR processing fails.</exception>
        /// <exception cref="OperationCanceledException">Thrown when polling is cancelled.</exception>
        Task<OCRStatusResponse> PollForCompletionWithConfigurationAsync(
            string trackingId,
            PollingConfiguration configuration,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to cancel an ongoing OCR processing operation.
        /// </summary>
        /// <param name="trackingId">The unique tracking identifier for the OCR submission.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous cancellation operation.
        /// The task result indicates whether cancellation was successful.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when trackingId is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the operation cannot be cancelled.</exception>
        Task<bool> CancelProcessingAsync(
            string trackingId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a status update during OCR polling operations.
    /// Provides detailed progress information for UI updates.
    /// </summary>
    public class OCRStatusUpdate
    {
        /// <summary>
        /// Current processing status.
        /// </summary>
        public OCRProcessingStatus Status { get; set; }

        /// <summary>
        /// Progress percentage (0-100).
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Human-readable status message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Estimated seconds remaining for completion.
        /// </summary>
        public int? EstimatedSecondsRemaining { get; set; }

        /// <summary>
        /// Queue position if still queued.
        /// </summary>
        public int? QueuePosition { get; set; }

        /// <summary>
        /// Number of polling attempts made so far.
        /// </summary>
        public int PollingAttempt { get; set; }

        /// <summary>
        /// Total elapsed time since polling started.
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Timestamp of this status update.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Creates an OCRStatusUpdate from an OCRStatusResponse.
        /// </summary>
        /// <param name="response">The OCR status response.</param>
        /// <param name="attempt">The current polling attempt number.</param>
        /// <param name="elapsed">The elapsed time since polling started.</param>
        /// <returns>A new OCRStatusUpdate instance.</returns>
        public static OCRStatusUpdate FromResponse(
            OCRStatusResponse response, 
            int attempt, 
            TimeSpan elapsed)
        {
            return new OCRStatusUpdate
            {
                Status = response.Status,
                ProgressPercentage = response.ProgressPercentage,
                Message = response.StatusMessage,
                EstimatedSecondsRemaining = response.EstimatedSecondsRemaining,
                QueuePosition = response.QueuePosition,
                PollingAttempt = attempt,
                ElapsedTime = elapsed
            };
        }
    }

    /// <summary>
    /// Exception thrown when OCR processing fails during polling.
    /// </summary>
    public class OCRProcessingException : Exception
    {
        /// <summary>
        /// Gets the tracking ID of the failed OCR operation.
        /// </summary>
        public string TrackingId { get; }

        /// <summary>
        /// Gets the error code from the OCR service if available.
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Gets the final status of the OCR operation.
        /// </summary>
        public OCRProcessingStatus FinalStatus { get; }

        /// <summary>
        /// Initializes a new instance of the OCRProcessingException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="trackingId">The tracking ID of the failed operation.</param>
        /// <param name="errorCode">The error code from the OCR service.</param>
        /// <param name="finalStatus">The final status of the operation.</param>
        public OCRProcessingException(
            string message, 
            string trackingId, 
            string errorCode = null,
            OCRProcessingStatus finalStatus = OCRProcessingStatus.Failed) 
            : base(message)
        {
            TrackingId = trackingId;
            ErrorCode = errorCode;
            FinalStatus = finalStatus;
        }

        /// <summary>
        /// Initializes a new instance of the OCRProcessingException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="trackingId">The tracking ID of the failed operation.</param>
        /// <param name="innerException">The inner exception.</param>
        public OCRProcessingException(
            string message, 
            string trackingId, 
            Exception innerException) 
            : base(message, innerException)
        {
            TrackingId = trackingId;
            FinalStatus = OCRProcessingStatus.Failed;
        }
    }
}