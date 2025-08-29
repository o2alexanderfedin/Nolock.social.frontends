using System.Text.RegularExpressions;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Validator for email fields.
    /// </summary>
    public class EmailFieldValidator : IFieldTypeValidator
    {
        private static readonly string EmailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

        public FieldType FieldType => FieldType.Email;

        public async Task ValidateAsync(object value, string fieldName, FieldValidationResult result)
        {
            var email = value.ToString()!;
            
            if (!Regex.IsMatch(email, EmailPattern, RegexOptions.IgnoreCase))
            {
                result.Errors.Add($"{fieldName} must be a valid email address.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }
    }
}