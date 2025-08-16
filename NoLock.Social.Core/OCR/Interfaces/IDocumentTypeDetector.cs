using System.Threading;
using System.Threading.Tasks;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Interfaces
{
    /// <summary>
    /// Defines the contract for automatic document type detection from OCR text.
    /// Provides confidence-based detection and fallback mechanisms.
    /// </summary>
    public interface IDocumentTypeDetector
    {
        /// <summary>
        /// Detects the document type from raw OCR text with confidence scoring.
        /// </summary>
        /// <param name="rawOcrData">The raw OCR text to analyze.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains the detection result with document type and confidence score.
        /// </returns>
        Task<DocumentTypeDetectionResult> DetectDocumentTypeAsync(
            string rawOcrData, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Detects multiple possible document types with their respective confidence scores.
        /// </summary>
        /// <param name="rawOcrData">The raw OCR text to analyze.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains multiple detection results ordered by confidence.
        /// </returns>
        Task<DocumentTypeDetectionResult[]> DetectMultipleDocumentTypesAsync(
            string rawOcrData,
            int maxResults = 3,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the minimum confidence threshold for automatic detection.
        /// Below this threshold, manual selection should be offered.
        /// </summary>
        double MinimumConfidenceThreshold { get; }

        /// <summary>
        /// Clears any cached detection results.
        /// </summary>
        void ClearCache();
    }
}