using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Document-specific validation rules for W4 tax form documents.
    /// </summary>
    public class W4SpecificRules : IDocumentSpecificRule
    {
        public string DocumentType => "w4";

        public bool CanHandle(string fieldName, string documentType)
        {
            return documentType.Equals(DocumentType, StringComparison.OrdinalIgnoreCase);
        }

        public async Task ApplyRulesAsync(string fieldName, object value, FieldValidationResult result)
        {
            // W4-specific validation rules would be implemented here
            // For now, this is a placeholder for future W4-specific rules
            await Task.CompletedTask;
        }
    }
}