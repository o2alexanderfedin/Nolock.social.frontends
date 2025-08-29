using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Validator for currency fields (inherits from DecimalFieldValidator for consistency).
    /// </summary>
    public class CurrencyFieldValidator : DecimalFieldValidator
    {
        public new FieldType FieldType => FieldType.Currency;
    }
}