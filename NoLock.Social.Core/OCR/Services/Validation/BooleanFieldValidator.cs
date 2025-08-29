using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Validator for boolean fields.
    /// </summary>
    public class BooleanFieldValidator : IFieldTypeValidator
    {
        private static readonly string[] ValidBooleanValues = { "true", "false", "yes", "no", "1", "0" };

        public FieldType FieldType => FieldType.Boolean;

        public async Task ValidateAsync(object value, string fieldName, FieldValidationResult result)
        {
            var stringValue = value.ToString()!.ToLower();
            
            if (!ValidBooleanValues.Contains(stringValue))
            {
                result.Errors.Add($"{fieldName} must be true/false or yes/no.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }
    }
}