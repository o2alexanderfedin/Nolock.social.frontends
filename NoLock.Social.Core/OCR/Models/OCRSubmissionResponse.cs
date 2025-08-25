namespace NoLock.Social.Core.OCR.Models
{
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