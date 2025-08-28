using CameraDocumentType = NoLock.Social.Core.Camera.Models.DocumentType;

namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents the result of document type detection with confidence scoring.
    /// </summary>
    public class DocumentTypeDetectionResult
    {
        /// <summary>
        /// Gets or sets the detected document type (e.g., "Receipt", "Check", "W4").
        /// </summary>
        public string DocumentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the confidence score for the detection (0.0 to 1.0).
        /// Higher values indicate higher confidence in the detection.
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Gets or sets whether the confidence is above the minimum threshold for automatic processing.
        /// </summary>
        public bool IsConfident { get; set; }

        /// <summary>
        /// Gets or sets the keywords that matched for this document type.
        /// </summary>
        public List<string> MatchedKeywords { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the total number of keyword matches found.
        /// </summary>
        public int KeywordMatchCount { get; set; }

        /// <summary>
        /// Gets or sets additional detection metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the detection method used (e.g., "Keyword", "Pattern", "ML").
        /// </summary>
        public string DetectionMethod { get; set; } = "Keyword";

        /// <summary>
        /// Gets or sets the timestamp when the detection was performed.
        /// </summary>
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets whether manual override is recommended due to low confidence.
        /// </summary>
        public bool RequiresManualConfirmation { get; set; }

        /// <summary>
        /// Gets or sets the reason for requiring manual confirmation, if applicable.
        /// </summary>
        public string ManualConfirmationReason { get; set; } = string.Empty;

        /// <summary>
        /// Creates a result for an unknown document type.
        /// </summary>
        /// <returns>A detection result indicating unknown document type.</returns>
        public static DocumentTypeDetectionResult Unknown()
        {
            return new DocumentTypeDetectionResult
            {
                DocumentType = CameraDocumentType.Other.ToString(),
                ConfidenceScore = 0.0,
                IsConfident = false,
                RequiresManualConfirmation = true,
                ManualConfirmationReason = "Could not determine document type from OCR text",
                DetectionMethod = "None"
            };
        }

        /// <summary>
        /// Creates a result for an ambiguous detection where multiple types are possible.
        /// </summary>
        /// <param name="possibleTypes">The possible document types.</param>
        /// <returns>A detection result indicating ambiguous detection.</returns>
        public static DocumentTypeDetectionResult Ambiguous(params string[] possibleTypes)
        {
            return new DocumentTypeDetectionResult
            {
                DocumentType = CameraDocumentType.Other.ToString(), // Using Other for ambiguous cases
                ConfidenceScore = 0.0,
                IsConfident = false,
                RequiresManualConfirmation = true,
                ManualConfirmationReason = $"Multiple possible document types detected: {string.Join(", ", possibleTypes)}",
                DetectionMethod = "Multiple",
                Metadata = { ["PossibleTypes"] = possibleTypes }
            };
        }

        /// <summary>
        /// Validates the detection result.
        /// </summary>
        /// <returns>True if the result is valid; otherwise, false.</returns>
        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(DocumentType))
            {
                return false;
            }

            if (ConfidenceScore < 0.0 || ConfidenceScore > 1.0)
            {
                return false;
            }

            if (DetectedAt > DateTime.UtcNow.AddMinutes(1))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns a string representation of the detection result.
        /// </summary>
        /// <returns>A string describing the detection result.</returns>
        public override string ToString()
        {
            return $"{DocumentType} (Confidence: {ConfidenceScore:P}, Method: {DetectionMethod})";
        }
    }
}