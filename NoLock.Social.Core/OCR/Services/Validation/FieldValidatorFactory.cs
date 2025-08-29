using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Factory for creating field validators based on field type.
    /// </summary>
    public class FieldValidatorFactory
    {
        private readonly Dictionary<FieldType, IFieldTypeValidator> _validators;

        public FieldValidatorFactory()
        {
            _validators = new Dictionary<FieldType, IFieldTypeValidator>
            {
                [FieldType.Decimal] = new DecimalFieldValidator(),
                [FieldType.Currency] = new CurrencyFieldValidator(),
                [FieldType.Integer] = new IntegerFieldValidator(),
                [FieldType.Date] = new DateFieldValidator(),
                [FieldType.DateTime] = new DateTimeFieldValidator(),
                [FieldType.Email] = new EmailFieldValidator(),
                [FieldType.Phone] = new PhoneFieldValidator(),
                [FieldType.RoutingNumber] = new RoutingNumberFieldValidator(),
                [FieldType.AccountNumber] = new AccountNumberFieldValidator(),
                [FieldType.PostalCode] = new PostalCodeFieldValidator(),
                [FieldType.Percentage] = new PercentageFieldValidator(),
                [FieldType.Boolean] = new BooleanFieldValidator(),
                [FieldType.Text] = new TextFieldValidator()
            };
        }

        /// <summary>
        /// Gets a validator for the specified field type.
        /// </summary>
        /// <param name="fieldType">The field type to validate.</param>
        /// <returns>The appropriate field validator.</returns>
        public IFieldTypeValidator GetValidator(FieldType fieldType)
        {
            return _validators.TryGetValue(fieldType, out var validator) 
                ? validator 
                : _validators[FieldType.Text]; // Default to text validator
        }
    }
}