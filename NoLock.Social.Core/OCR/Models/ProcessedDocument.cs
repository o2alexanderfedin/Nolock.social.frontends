namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Base class for all processed document results.
    /// Provides common properties for document metadata and processing information.
    /// </summary>
    public abstract class ProcessedDocument
    {
        /// <summary>
        /// Gets or sets the unique identifier for this processed document.
        /// This will be the SHA-256 hash when stored in CAS.
        /// </summary>
        public string DocumentId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of document (e.g., "Receipt", "W2", "W4", "1099", "Check").
        /// </summary>
        public string DocumentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the document was processed.
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the confidence score of the OCR extraction (0.0 to 1.0).
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Gets or sets the original file name if available.
        /// </summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the MIME type of the original document.
        /// </summary>
        public string MimeType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the size of the original document in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the raw OCR text extracted from the document.
        /// </summary>
        public string RawOcrText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets any processing warnings encountered.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets any validation errors encountered.
        /// </summary>
        public List<string> ValidationErrors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets additional metadata as key-value pairs.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets whether the document has been digitally signed.
        /// </summary>
        public bool IsSigned { get; set; }

        /// <summary>
        /// Gets or sets the digital signature if the document has been signed.
        /// </summary>
        public string DigitalSignature { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the public key used to verify the signature.
        /// </summary>
        public string SignaturePublicKey { get; set; } = string.Empty;

        /// <summary>
        /// Validates the processed document data.
        /// </summary>
        /// <returns>True if the document data is valid; otherwise, false.</returns>
        public virtual bool Validate()
        {
            ValidationErrors.Clear();

            if (string.IsNullOrWhiteSpace(DocumentType))
            {
                ValidationErrors.Add("Document type is required.");
            }

            if (ConfidenceScore < 0 || ConfidenceScore > 1)
            {
                ValidationErrors.Add("Confidence score must be between 0 and 1.");
            }

            if (ProcessedAt > DateTime.UtcNow.AddMinutes(1))
            {
                ValidationErrors.Add("Processed date cannot be in the future.");
            }

            return ValidationErrors.Count == 0;
        }
    }

    /// <summary>
    /// Represents a processed receipt document.
    /// </summary>
    public class ProcessedReceipt : ProcessedDocument
    {
        /// <summary>
        /// Gets or sets the extracted receipt data.
        /// </summary>
        public ReceiptData ReceiptData { get; set; } = new ReceiptData();

        /// <summary>
        /// Initializes a new instance of the ProcessedReceipt class.
        /// </summary>
        public ProcessedReceipt()
        {
            DocumentType = "Receipt";
        }

        /// <summary>
        /// Validates the processed receipt including receipt-specific validation.
        /// </summary>
        /// <returns>True if the receipt data is valid; otherwise, false.</returns>
        public override bool Validate()
        {
            // Run base validation but continue with receipt-specific validation
            bool baseIsValid = base.Validate();

            if (ReceiptData == null)
            {
                ValidationErrors.Add("Receipt data is required.");
                return false;
            }

            // Validate receipt totals
            if (ReceiptData.Total < 0)
            {
                ValidationErrors.Add("Total amount cannot be negative.");
            }

            if (ReceiptData.Subtotal < 0)
            {
                ValidationErrors.Add("Subtotal amount cannot be negative.");
            }

            if (ReceiptData.TaxAmount < 0)
            {
                ValidationErrors.Add("Tax amount cannot be negative.");
            }

            // Validate that total equals subtotal + tax (with small tolerance for rounding)
            var calculatedTotal = ReceiptData.Subtotal + ReceiptData.TaxAmount;
            if (Math.Abs(calculatedTotal - ReceiptData.Total) >= 0.01m)
            {
                ValidationErrors.Add($"Total mismatch: Expected {calculatedTotal:C} but got {ReceiptData.Total:C}");
            }

            return ValidationErrors.Count == 0;
        }
    }

    /// <summary>
    /// Represents a processed check document.
    /// </summary>
    public class ProcessedCheck : ProcessedDocument
    {
        /// <summary>
        /// Gets or sets the extracted check data.
        /// </summary>
        public CheckData CheckData { get; set; } = new CheckData();

        /// <summary>
        /// Initializes a new instance of the ProcessedCheck class.
        /// </summary>
        public ProcessedCheck()
        {
            DocumentType = "Check";
        }

        /// <summary>
        /// Validates the processed check including check-specific validation.
        /// </summary>
        /// <returns>True if the check data is valid; otherwise, false.</returns>
        public override bool Validate()
        {
            // Run base validation but continue with check-specific validation
            bool baseIsValid = base.Validate();

            if (CheckData == null)
            {
                ValidationErrors.Add("Check data is required.");
                return false;
            }

            // Delegate to CheckData's validation
            if (!CheckData.Validate())
            {
                ValidationErrors.AddRange(CheckData.ValidationErrors);
            }

            // Additional check-specific validations
            if (!CheckData.IsRoutingNumberValid)
            {
                ValidationErrors.Add("Invalid routing number checksum.");
            }

            if (!CheckData.AmountsMatch)
            {
                Warnings.Add("Written and numeric amounts do not match.");
            }

            if (!CheckData.SignatureDetected)
            {
                Warnings.Add("No signature detected on check.");
            }

            return ValidationErrors.Count == 0;
        }
    }
}