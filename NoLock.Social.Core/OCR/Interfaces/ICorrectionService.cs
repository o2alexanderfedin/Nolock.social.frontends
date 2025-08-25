using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Interfaces
{
    /// <summary>
    /// Interface for managing OCR field corrections and validation.
    /// Provides functionality to track user corrections, validate field values,
    /// and update confidence scores based on corrections.
    /// </summary>
    public interface ICorrectionService
    {
        /// <summary>
        /// Creates a correctable copy of a processed document.
        /// </summary>
        /// <param name="document">The original processed document.</param>
        /// <returns>A correctable wrapper containing the document and correction tracking.</returns>
        Task<CorrectedProcessedDocument> CreateCorrectableDocumentAsync(ProcessedDocument document);

        /// <summary>
        /// Applies a field correction to the document.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="fieldName">The name of the field being corrected.</param>
        /// <param name="newValue">The corrected value.</param>
        /// <param name="originalValue">The original OCR value.</param>
        /// <param name="originalConfidence">The original confidence score.</param>
        /// <returns>A validation result indicating success or errors.</returns>
        Task<FieldValidationResult> ApplyFieldCorrectionAsync(
            string documentId,
            string fieldName, 
            object newValue, 
            object originalValue, 
            double originalConfidence);

        /// <summary>
        /// Validates a field value based on its type and constraints.
        /// </summary>
        /// <param name="fieldName">The name of the field to validate.</param>
        /// <param name="value">The value to validate.</param>
        /// <param name="documentType">The type of document (Receipt, Check, etc.).</param>
        /// <returns>A validation result with any errors or warnings.</returns>
        Task<FieldValidationResult> ValidateFieldAsync(string fieldName, object value, string documentType);

        /// <summary>
        /// Calculates an updated confidence score based on user correction.
        /// </summary>
        /// <param name="originalConfidence">The original OCR confidence score.</param>
        /// <param name="wasValueChanged">Whether the user changed the value.</param>
        /// <param name="validationResult">The validation result for the corrected value.</param>
        /// <returns>The updated confidence score.</returns>
        double CalculateUpdatedConfidence(double originalConfidence, bool wasValueChanged, FieldValidationResult validationResult);

        /// <summary>
        /// Gets all corrections made to a document.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <returns>A collection of field corrections.</returns>
        Task<IReadOnlyList<FieldCorrection>> GetCorrectionsAsync(string documentId);

        /// <summary>
        /// Saves all pending corrections to the document.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <returns>A task representing the save operation.</returns>
        Task<ProcessedDocument> SaveCorrectionsAsync(string documentId);

        /// <summary>
        /// Cancels all pending corrections for a document.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <returns>A task representing the cancel operation.</returns>
        Task CancelCorrectionsAsync(string documentId);

        /// <summary>
        /// Reverts a specific field correction.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="fieldName">The field name to revert.</param>
        /// <returns>A task representing the revert operation.</returns>
        Task RevertFieldCorrectionAsync(string documentId, string fieldName);

        /// <summary>
        /// Identifies fields with low confidence that should be highlighted for correction.
        /// </summary>
        /// <param name="document">The processed document.</param>
        /// <param name="confidenceThreshold">The confidence threshold below which fields should be highlighted.</param>
        /// <returns>A collection of field names with low confidence.</returns>
        Task<IReadOnlyList<string>> GetLowConfidenceFieldsAsync(ProcessedDocument document, double confidenceThreshold = 0.7);

        /// <summary>
        /// Gets the validation rules for a specific field type.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <param name="documentType">The document type.</param>
        /// <returns>The validation rules for the field.</returns>
        Task<FieldValidationRules> GetFieldValidationRulesAsync(string fieldName, string documentType);
    }
}