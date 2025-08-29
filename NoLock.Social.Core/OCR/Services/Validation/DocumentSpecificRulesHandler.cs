using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Handler for applying document-specific validation rules using Chain of Responsibility pattern.
    /// </summary>
    public class DocumentSpecificRulesHandler
    {
        private readonly List<IDocumentSpecificRule> _rules;

        public DocumentSpecificRulesHandler()
        {
            _rules = new List<IDocumentSpecificRule>
            {
                new ReceiptSpecificRules(),
                new CheckSpecificRules(),
                new W4SpecificRules()
            };
        }

        /// <summary>
        /// Applies document-specific validation rules for a field.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <param name="value">The field value.</param>
        /// <param name="documentType">The document type.</param>
        /// <param name="result">The validation result to update.</param>
        /// <returns>A task representing the validation operation.</returns>
        public async Task ApplyRulesAsync(string fieldName, object value, string documentType, FieldValidationResult result)
        {
            foreach (var rule in _rules)
            {
                if (rule.CanHandle(fieldName, documentType))
                {
                    await rule.ApplyRulesAsync(fieldName, value, result);
                    break; // Apply only the first matching rule
                }
            }
        }
    }
}