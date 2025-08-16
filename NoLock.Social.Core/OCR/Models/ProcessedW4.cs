using System;

namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents a processed W-4 tax withholding form document.
    /// </summary>
    public class ProcessedW4 : ProcessedDocument
    {
        /// <summary>
        /// Gets or sets the extracted W-4 form data.
        /// </summary>
        public W4Data W4Data { get; set; } = new W4Data();

        /// <summary>
        /// Initializes a new instance of the ProcessedW4 class.
        /// </summary>
        public ProcessedW4()
        {
            DocumentType = "W4";
        }

        /// <summary>
        /// Validates the processed W-4 including W-4-specific validation.
        /// </summary>
        /// <returns>True if the W-4 data is valid; otherwise, false.</returns>
        public override bool Validate()
        {
            if (!base.Validate())
            {
                return false;
            }

            if (W4Data == null)
            {
                ValidationErrors.Add("W-4 data is required.");
                return false;
            }

            // Delegate to W4Data's validation
            if (!W4Data.Validate())
            {
                ValidationErrors.AddRange(W4Data.ValidationErrors);
            }

            // Additional W-4-specific validations
            if (!W4Data.SignatureDetected)
            {
                Warnings.Add("No signature detected on W-4 form.");
            }

            if (W4Data.DateSigned.HasValue && W4Data.DateSigned.Value > DateTime.UtcNow)
            {
                ValidationErrors.Add("Date signed cannot be in the future.");
            }

            // Warn if using old format
            if (W4Data.IsPreTwentyTwentyFormat)
            {
                Warnings.Add($"This W-4 form uses the pre-2020 format (version {W4Data.FormVersion}). Consider updating to the current format.");
            }

            // Validate SSN is masked for security
            if (!string.IsNullOrWhiteSpace(W4Data.SSN) && !W4Data.SSN.Contains("X") && !W4Data.SSN.Contains("*"))
            {
                // Automatically mask the SSN for security
                W4Data.SSN = W4Data.GetMaskedSSN();
                Warnings.Add("SSN has been masked for security.");
            }

            return ValidationErrors.Count == 0;
        }
    }
}