using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Validator for integer fields.
    /// </summary>
    public class IntegerFieldValidator : IFieldTypeValidator
    {
        public FieldType FieldType => FieldType.Integer;

        public async Task ValidateAsync(object value, string fieldName, FieldValidationResult result)
        {
            if (!int.TryParse(value.ToString(), out var intValue))
            {
                result.Errors.Add($"{fieldName} must be a valid whole number.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }
    }
}