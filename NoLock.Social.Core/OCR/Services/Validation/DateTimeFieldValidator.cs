using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Validator for datetime fields (inherits from DateFieldValidator for consistency).
    /// </summary>
    public class DateTimeFieldValidator : DateFieldValidator
    {
        public new FieldType FieldType => FieldType.DateTime;
    }
}