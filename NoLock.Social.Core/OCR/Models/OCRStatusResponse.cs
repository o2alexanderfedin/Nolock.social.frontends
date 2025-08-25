namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents the response from checking the status of an OCR processing request.
    /// Contains detailed status information including progress, timing, and result data.
    /// </summary>
    public class OCRStatusResponse
    {
        /// <summary>
        /// Unique tracking identifier for the OCR submission.
        /// </summary>
        public string TrackingId { get; set; }

        /// <summary>
        /// Current status of the OCR processing.
        /// </summary>
        public OCRProcessingStatus Status { get; set; }

        /// <summary>
        /// Progress percentage of the OCR processing (0-100).
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Timestamp when the document was submitted for processing.
        /// </summary>
        public DateTime SubmittedAt { get; set; }

        /// <summary>
        /// Timestamp when the processing started.
        /// Null if processing hasn't started yet.
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Timestamp when the processing completed.
        /// Null if processing hasn't completed yet.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Estimated time remaining for completion in seconds.
        /// Null if estimation is not available.
        /// </summary>
        public int? EstimatedSecondsRemaining { get; set; }

        /// <summary>
        /// Detailed status message providing additional context.
        /// </summary>
        public string StatusMessage { get; set; }

        /// <summary>
        /// Queue position if the document is still queued.
        /// Null if not in queue or position is unknown.
        /// </summary>
        public int? QueuePosition { get; set; }

        /// <summary>
        /// Error message if processing failed.
        /// Null if no error occurred.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Error code if processing failed.
        /// Null if no error occurred.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Indicates whether the result can be retrieved.
        /// True when status is Complete.
        /// </summary>
        public bool IsResultAvailable => Status == OCRProcessingStatus.Complete;

        /// <summary>
        /// Indicates whether processing can be cancelled.
        /// True when status is Queued or Processing.
        /// </summary>
        public bool IsCancellable => Status == OCRProcessingStatus.Queued || 
                                     Status == OCRProcessingStatus.Processing;

        /// <summary>
        /// URL to retrieve the processed document result.
        /// Null if result is not yet available.
        /// </summary>
        public string ResultUrl { get; set; }

        /// <summary>
        /// Processed document data if available and included in response.
        /// Null if not yet available or not included.
        /// </summary>
        public OCRResultData ResultData { get; set; }

        /// <summary>
        /// Additional metadata about the processing.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Represents the extracted data from a processed OCR document.
    /// </summary>
    public class OCRResultData
    {
        /// <summary>
        /// Extracted text content from the document.
        /// </summary>
        public string ExtractedText { get; set; }

        /// <summary>
        /// Confidence score of the OCR extraction (0-100).
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Detected language of the document.
        /// </summary>
        public string DetectedLanguage { get; set; }

        /// <summary>
        /// Number of pages processed in the document.
        /// </summary>
        public int PageCount { get; set; }

        /// <summary>
        /// Structured data extracted from the document (JSON format).
        /// </summary>
        public string StructuredData { get; set; }

        /// <summary>
        /// List of detected entities or fields in the document.
        /// </summary>
        public List<ExtractedField> ExtractedFields { get; set; } = new List<ExtractedField>();

        /// <summary>
        /// Processing statistics and metrics.
        /// </summary>
        public ProcessingMetrics Metrics { get; set; }
    }

    /// <summary>
    /// Represents an extracted field or entity from the OCR document.
    /// </summary>
    public class ExtractedField
    {
        /// <summary>
        /// Name or type of the field.
        /// </summary>
        public string FieldName { get; set; }

        /// <summary>
        /// Extracted value of the field.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Confidence score for this specific field (0-100).
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Bounding box coordinates if available.
        /// </summary>
        public BoundingBox BoundingBox { get; set; }
    }

    /// <summary>
    /// Represents bounding box coordinates for an extracted element.
    /// </summary>
    public class BoundingBox
    {
        /// <summary>
        /// Top-left X coordinate.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Top-left Y coordinate.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Width of the bounding box.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Height of the bounding box.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Page number where this bounding box is located.
        /// </summary>
        public int PageNumber { get; set; }
    }

    /// <summary>
    /// Processing metrics and statistics.
    /// </summary>
    public class ProcessingMetrics
    {
        /// <summary>
        /// Time taken for OCR processing in milliseconds.
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Number of characters extracted.
        /// </summary>
        public int CharacterCount { get; set; }

        /// <summary>
        /// Number of words extracted.
        /// </summary>
        public int WordCount { get; set; }

        /// <summary>
        /// Number of lines extracted.
        /// </summary>
        public int LineCount { get; set; }

        /// <summary>
        /// Image quality score (0-100).
        /// </summary>
        public double ImageQualityScore { get; set; }
    }
}