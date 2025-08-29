using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Validator for text fields (default validator).
    /// </summary>
    public class TextFieldValidator : IFieldTypeValidator
    {
        private static readonly string[] SuspiciousCharacters = { "<", ">", "script" };

        public FieldType FieldType => FieldType.Text;

        public async Task ValidateAsync(object value, string fieldName, FieldValidationResult result)
        {
            var text = value.ToString()!;
            
            CheckForSuspiciousContent(text, fieldName, result);

            await Task.CompletedTask;
        }

        private static void CheckForSuspiciousContent(string text, string fieldName, FieldValidationResult result)
        {
            if (SuspiciousCharacters.Any(suspicious => text.Contains(suspicious)))
            {
                result.Warnings.Add($"{fieldName} contains special characters that may need review.");
            }
        }
    }
}