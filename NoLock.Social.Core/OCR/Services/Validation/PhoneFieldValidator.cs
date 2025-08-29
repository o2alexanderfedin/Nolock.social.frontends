using System.Text.RegularExpressions;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Validator for phone number fields.
    /// </summary>
    public class PhoneFieldValidator : IFieldTypeValidator
    {
        private static readonly string PhonePattern = @"^(\+?1[-.\s]?)?\(?([0-9]{3})\)?[-.\s]?([0-9]{3})[-.\s]?([0-9]{4})$";

        public FieldType FieldType => FieldType.Phone;

        public async Task ValidateAsync(object value, string fieldName, FieldValidationResult result)
        {
            var phone = value.ToString()!;
            
            if (!Regex.IsMatch(phone, PhonePattern))
            {
                result.Errors.Add($"{fieldName} must be a valid phone number format.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }
    }
}