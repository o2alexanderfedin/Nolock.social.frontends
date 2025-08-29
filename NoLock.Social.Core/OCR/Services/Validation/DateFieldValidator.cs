using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Validator for date and datetime fields.
    /// </summary>
    public class DateFieldValidator : IFieldTypeValidator
    {
        public FieldType FieldType => FieldType.Date;

        public async Task ValidateAsync(object value, string fieldName, FieldValidationResult result)
        {
            if (value == null || !DateTime.TryParse(value.ToString(), out var dateValue))
            {
                result.Errors.Add($"{fieldName} must be a valid date.");
                result.IsValid = false;
                return;
            }

            ValidateDateRange(dateValue, fieldName, result);

            await Task.CompletedTask;
        }

        private static void ValidateDateRange(DateTime dateValue, string fieldName, FieldValidationResult result)
        {
            var now = DateTime.Now;
            
            if (dateValue > now.AddDays(1))
            {
                result.Warnings.Add($"{fieldName} is in the future. Please verify the date is correct.");
            }

            if (dateValue < now.AddYears(-10))
            {
                result.Warnings.Add($"{fieldName} is more than 10 years old. Please verify the date is correct.");
            }
        }
    }
}