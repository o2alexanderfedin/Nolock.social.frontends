namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents a user correction made to an OCR field.
    /// Tracks the original value, corrected value, confidence changes, and validation status.
    /// </summary>
    public class FieldCorrection
    {
        /// <summary>
        /// Gets or sets the name of the field that was corrected.
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the original OCR-extracted value.
        /// </summary>
        public object? OriginalValue { get; set; }

        /// <summary>
        /// Gets or sets the user-corrected value.
        /// </summary>
        public object? CorrectedValue { get; set; }

        /// <summary>
        /// Gets or sets the original confidence score from OCR (0.0 to 1.0).
        /// </summary>
        public double OriginalConfidence { get; set; }

        /// <summary>
        /// Gets or sets the updated confidence score after correction (0.0 to 1.0).
        /// </summary>
        public double UpdatedConfidence { get; set; }

        /// <summary>
        /// Gets or sets when the correction was made.
        /// </summary>
        public DateTime CorrectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets whether the corrected value is valid.
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Gets or sets any validation errors for the corrected value.
        /// </summary>
        public List<string> ValidationErrors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets any validation warnings for the corrected value.
        /// </summary>
        public List<string> ValidationWarnings { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets whether this correction has been saved to the document.
        /// </summary>
        public bool IsSaved { get; set; } = false;

        /// <summary>
        /// Gets or sets the type of the field (for validation purposes).
        /// </summary>
        public FieldType FieldType { get; set; } = FieldType.Text;

        /// <summary>
        /// Gets or sets additional metadata about the correction.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets whether the value was actually changed by the user.
        /// </summary>
        public bool WasValueChanged => !Equals(OriginalValue, CorrectedValue);

        /// <summary>
        /// Gets whether this correction improved the confidence score.
        /// </summary>
        public bool ImprovedConfidence => UpdatedConfidence > OriginalConfidence;
    }

    /// <summary>
    /// Represents the validation result for a field correction.
    /// </summary>
    public class FieldValidationResult
    {
        /// <summary>
        /// Gets or sets whether the field value is valid.
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Gets or sets any validation errors.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets any validation warnings.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets additional validation metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets whether there are any validation issues (errors or warnings).
        /// </summary>
        public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0;

        /// <summary>
        /// Creates a successful validation result.
        /// </summary>
        /// <returns>A validation result indicating success.</returns>
        public static FieldValidationResult Success()
        {
            return new FieldValidationResult { IsValid = true };
        }

        /// <summary>
        /// Creates a failed validation result with errors.
        /// </summary>
        /// <param name="errors">The validation errors.</param>
        /// <returns>A validation result indicating failure.</returns>
        public static FieldValidationResult Failure(params string[] errors)
        {
            return new FieldValidationResult 
            { 
                IsValid = false, 
                Errors = new List<string>(errors) 
            };
        }

        /// <summary>
        /// Creates a validation result with warnings.
        /// </summary>
        /// <param name="warnings">The validation warnings.</param>
        /// <returns>A validation result with warnings.</returns>
        public static FieldValidationResult WithWarnings(params string[] warnings)
        {
            return new FieldValidationResult 
            { 
                IsValid = true, 
                Warnings = new List<string>(warnings) 
            };
        }
    }

    /// <summary>
    /// Represents validation rules for a specific field type.
    /// </summary>
    public class FieldValidationRules
    {
        /// <summary>
        /// Gets or sets the field name these rules apply to.
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the field type.
        /// </summary>
        public FieldType FieldType { get; set; } = FieldType.Text;

        /// <summary>
        /// Gets or sets whether the field is required.
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// Gets or sets the minimum length for text fields.
        /// </summary>
        public int? MinLength { get; set; }

        /// <summary>
        /// Gets or sets the maximum length for text fields.
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Gets or sets the minimum value for numeric fields.
        /// </summary>
        public decimal? MinValue { get; set; }

        /// <summary>
        /// Gets or sets the maximum value for numeric fields.
        /// </summary>
        public decimal? MaxValue { get; set; }

        /// <summary>
        /// Gets or sets a regular expression pattern for validation.
        /// </summary>
        public string? Pattern { get; set; }

        /// <summary>
        /// Gets or sets a custom validation message.
        /// </summary>
        public string? ValidationMessage { get; set; }

        /// <summary>
        /// Gets or sets additional validation options.
        /// </summary>
        public Dictionary<string, object> Options { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Defines the types of fields that can be corrected.
    /// </summary>
    public enum FieldType
    {
        /// <summary>
        /// Plain text field.
        /// </summary>
        Text,

        /// <summary>
        /// Numeric decimal field.
        /// </summary>
        Decimal,

        /// <summary>
        /// Integer field.
        /// </summary>
        Integer,

        /// <summary>
        /// Date field.
        /// </summary>
        Date,

        /// <summary>
        /// DateTime field.
        /// </summary>
        DateTime,

        /// <summary>
        /// Email address field.
        /// </summary>
        Email,

        /// <summary>
        /// Phone number field.
        /// </summary>
        Phone,

        /// <summary>
        /// Currency amount field.
        /// </summary>
        Currency,

        /// <summary>
        /// Percentage field.
        /// </summary>
        Percentage,

        /// <summary>
        /// Boolean field.
        /// </summary>
        Boolean,

        /// <summary>
        /// Bank routing number field.
        /// </summary>
        RoutingNumber,

        /// <summary>
        /// Bank account number field.
        /// </summary>
        AccountNumber,

        /// <summary>
        /// Postal/ZIP code field.
        /// </summary>
        PostalCode
    }
}