using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Interface for document-specific validation rules using Chain of Responsibility pattern.
    /// </summary>
    public interface IDocumentSpecificRule
    {
        /// <summary>
        /// Gets the document type this rule applies to.
        /// </summary>
        string DocumentType { get; }

        /// <summary>
        /// Checks if this rule can handle the given field and document type.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <param name="documentType">The document type.</param>
        /// <returns>True if this rule can handle the field/document combination.</returns>
        bool CanHandle(string fieldName, string documentType);

        /// <summary>
        /// Applies document-specific validation rules to a field.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <param name="value">The field value.</param>
        /// <param name="result">The validation result to update.</param>
        /// <returns>A task representing the validation operation.</returns>
        Task ApplyRulesAsync(string fieldName, object value, FieldValidationResult result);
    }
}