using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Interface for field type-specific validators using Strategy pattern.
    /// </summary>
    public interface IFieldTypeValidator
    {
        /// <summary>
        /// Gets the field type this validator handles.
        /// </summary>
        FieldType FieldType { get; }

        /// <summary>
        /// Validates a field value for the specific type.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="fieldName">The field name for context.</param>
        /// <param name="result">The validation result to update.</param>
        /// <returns>A task representing the validation operation.</returns>
        Task ValidateAsync(object value, string fieldName, FieldValidationResult result);
    }
}