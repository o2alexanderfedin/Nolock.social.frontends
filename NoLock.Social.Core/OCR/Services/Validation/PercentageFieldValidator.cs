using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Validator for percentage fields.
    /// </summary>
    public class PercentageFieldValidator : IFieldTypeValidator
    {
        public FieldType FieldType => FieldType.Percentage;

        public async Task ValidateAsync(object value, string fieldName, FieldValidationResult result)
        {
            if (!decimal.TryParse(value.ToString(), out var percentage))
            {
                result.Errors.Add($"{fieldName} must be a valid percentage.");
                result.IsValid = false;
                return;
            }

            if (percentage < 0 || percentage > 100)
            {
                result.Errors.Add($"{fieldName} must be between 0 and 100.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }
    }
}