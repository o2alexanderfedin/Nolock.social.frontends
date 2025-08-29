using System.Text.RegularExpressions;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Validator for account number fields.
    /// </summary>
    public class AccountNumberFieldValidator : IFieldTypeValidator
    {
        private static readonly string AccountNumberPattern = @"^[0-9]{4,17}$";

        public FieldType FieldType => FieldType.AccountNumber;

        public async Task ValidateAsync(object value, string fieldName, FieldValidationResult result)
        {
            var accountNumber = value.ToString()!;
            
            if (!Regex.IsMatch(accountNumber, AccountNumberPattern))
            {
                result.Errors.Add($"{fieldName} must be 4-17 digits.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }
    }
}