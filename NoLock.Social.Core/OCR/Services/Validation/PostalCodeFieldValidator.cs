using System.Text.RegularExpressions;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Validator for postal code fields.
    /// </summary>
    public class PostalCodeFieldValidator : IFieldTypeValidator
    {
        private static readonly string PostalCodePattern = @"^[0-9]{5}(-[0-9]{4})?$";

        public FieldType FieldType => FieldType.PostalCode;

        public async Task ValidateAsync(object value, string fieldName, FieldValidationResult result)
        {
            var postalCode = value.ToString()!;
            
            if (!Regex.IsMatch(postalCode, PostalCodePattern))
            {
                result.Errors.Add($"{fieldName} must be a valid ZIP code (e.g., 12345 or 12345-6789).");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }
    }
}