using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Validator for decimal and currency fields.
    /// </summary>
    public class DecimalFieldValidator : IFieldTypeValidator
    {
        public FieldType FieldType => FieldType.Decimal;

        public async Task ValidateAsync(object value, string fieldName, FieldValidationResult result)
        {
            if (!decimal.TryParse(value.ToString(), out var decimalValue))
            {
                result.Errors.Add($"{fieldName} must be a valid number.");
                result.IsValid = false;
                return;
            }

            if (IsCurrencyField(fieldName))
            {
                ValidateCurrencyAmount(decimalValue, fieldName, result);
            }

            await Task.CompletedTask;
        }

        private static bool IsCurrencyField(string fieldName)
        {
            var lowerName = fieldName.ToLower();
            return lowerName.Contains("amount") || lowerName.Contains("total") || lowerName.Contains("price");
        }

        private static void ValidateCurrencyAmount(decimal amount, string fieldName, FieldValidationResult result)
        {
            if (amount < 0)
            {
                result.Errors.Add($"{fieldName} cannot be negative.");
                result.IsValid = false;
            }

            if (amount > 1_000_000)
            {
                result.Warnings.Add($"{fieldName} amount ${amount:N2} is unusually large. Please verify.");
            }

            if (Math.Round(amount, 2) != amount)
            {
                result.Warnings.Add($"{fieldName} has more than 2 decimal places. Currency amounts typically use 2 decimal places.");
            }
        }
    }
}