using System;

namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents the response from submitting a document for OCR processing
    /// </summary>
    public class OCRSubmissionResponse
    {
        /// <summary>
        /// Unique tracking identifier for the OCR submission
        /// </summary>
        public string TrackingId { get; set; }

        /// <summary>
        /// Current status of the OCR processing
        /// </summary>
        public OCRProcessingStatus Status { get; set; }

        /// <summary>
        /// Timestamp when the document was submitted
        /// </summary>
        public DateTime SubmittedAt { get; set; }

        /// <summary>
        /// Estimated time when the OCR processing will be complete
        /// </summary>
        public DateTime? EstimatedCompletionTime { get; set; }
    }

    /// <summary>
    /// Enumeration of OCR processing status values
    /// </summary>
    public enum OCRProcessingStatus
    {
        /// <summary>
        /// Document is queued for processing
        /// </summary>
        Queued = 0,

        /// <summary>
        /// Document is currently being processed
        /// </summary>
        Processing = 1,

        /// <summary>
        /// OCR processing has completed successfully
        /// </summary>
        Complete = 2,

        /// <summary>
        /// OCR processing failed
        /// </summary>
        Failed = 3
    }
}