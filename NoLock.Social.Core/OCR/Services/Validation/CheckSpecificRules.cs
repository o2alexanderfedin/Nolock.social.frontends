using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Document-specific validation rules for check documents.
    /// </summary>
    public class CheckSpecificRules : IDocumentSpecificRule
    {
        public string DocumentType => "check";

        public bool CanHandle(string fieldName, string documentType)
        {
            return documentType.Equals(DocumentType, StringComparison.OrdinalIgnoreCase);
        }

        public async Task ApplyRulesAsync(string fieldName, object value, FieldValidationResult result)
        {
            var lowerFieldName = fieldName.ToLower();

            switch (lowerFieldName)
            {
                case "amount":
                    ValidateCheckAmount(value, fieldName, result);
                    break;

                case "date":
                    ValidateCheckDate(value, fieldName, result);
                    break;
            }

            await Task.CompletedTask;
        }

        private static void ValidateCheckAmount(object value, string fieldName, FieldValidationResult result)
        {
            if (decimal.TryParse(value.ToString(), out var amount))
            {
                if (amount <= 0)
                {
                    result.Errors.Add("Check amount must be greater than zero.");
                    result.IsValid = false;
                }

                if (amount > 10000)
                {
                    result.Warnings.Add("Check amount is unusually large. Please verify.");
                }
            }
        }

        private static void ValidateCheckDate(object value, string fieldName, FieldValidationResult result)
        {
            if (DateTime.TryParse(value.ToString(), out var checkDate))
            {
                if (checkDate > DateTime.Now.AddDays(180))
                {
                    result.Warnings.Add("Check is post-dated more than 6 months in the future.");
                }
            }
        }
    }
}