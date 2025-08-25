namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents a request to submit a document for OCR processing
    /// </summary>
    public class OCRSubmissionRequest
    {
        /// <summary>
        /// The image data as a base64 encoded string
        /// </summary>
        public byte[] ImageData { get; set; }

        /// <summary>
        /// The type of document being submitted for OCR
        /// </summary>
        public DocumentType DocumentType { get; set; }
    }

    /// <summary>
    /// Enumeration of supported document types for OCR processing
    /// </summary>
    public enum DocumentType
    {
        /// <summary>
        /// Receipt from purchase
        /// </summary>
        Receipt = 0,

        /// <summary>
        /// Bank check
        /// </summary>
        Check = 1
    }
}