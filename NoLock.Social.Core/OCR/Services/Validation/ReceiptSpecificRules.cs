using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Document-specific validation rules for receipt documents.
    /// </summary>
    public class ReceiptSpecificRules : IDocumentSpecificRule
    {
        public string DocumentType => "receipt";

        public bool CanHandle(string fieldName, string documentType)
        {
            return documentType.Equals(DocumentType, StringComparison.OrdinalIgnoreCase);
        }

        public async Task ApplyRulesAsync(string fieldName, object value, FieldValidationResult result)
        {
            var lowerFieldName = fieldName.ToLower();

            switch (lowerFieldName)
            {
                case "total":
                case "subtotal":
                case "taxamount":
                    ValidateReceiptAmount(value, fieldName, result);
                    break;

                case "transactiondate":
                    ValidateTransactionDate(value, fieldName, result);
                    break;
            }

            await Task.CompletedTask;
        }

        private static void ValidateReceiptAmount(object value, string fieldName, FieldValidationResult result)
        {
            if (decimal.TryParse(value.ToString(), out var amount) && amount < 0)
            {
                result.Errors.Add($"{fieldName} cannot be negative on a receipt.");
                result.IsValid = false;
            }
        }

        private static void ValidateTransactionDate(object value, string fieldName, FieldValidationResult result)
        {
            if (DateTime.TryParse(value.ToString(), out var date) && date > DateTime.Now)
            {
                result.Warnings.Add("Transaction date is in the future.");
            }
        }
    }
}