using System;
using System.Threading;
using System.Threading.Tasks;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Interfaces
{
    /// <summary>
    /// Defines the contract for OCR service operations with Mistral OCR API.
    /// Provides document submission and processing capabilities with full model parsing.
    /// </summary>
    public interface IOCRService
    {
        /// <summary>
        /// Submits a document for OCR processing to the Mistral OCR API.
        /// </summary>
        /// <param name="request">The OCR submission request containing document data and metadata.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. 
        /// The task result contains the OCR submission response with fully parsed models
        /// including tracking ID and processed document data.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
        /// <exception cref="OCRServiceException">Thrown when OCR processing fails.</exception>
        Task<OCRSubmissionResponse> SubmitDocumentAsync(
            OCRSubmissionRequest request, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current processing status of an OCR submission by tracking ID.
        /// </summary>
        /// <param name="trackingId">The unique tracking identifier for the OCR submission.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains the OCR status response with current processing state,
        /// progress information, and estimated completion time.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when trackingId is null or empty.</exception>
        /// <exception cref="OCRServiceException">Thrown when status retrieval fails.</exception>
        Task<OCRStatusResponse> GetStatusAsync(
            string trackingId,
            CancellationToken cancellationToken = default);
    }
}