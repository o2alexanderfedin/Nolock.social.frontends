using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Validator for routing number fields.
    /// </summary>
    public class RoutingNumberFieldValidator : IFieldTypeValidator
    {
        public FieldType FieldType => FieldType.RoutingNumber;

        public async Task ValidateAsync(object value, string fieldName, FieldValidationResult result)
        {
            var routingNumber = value.ToString()!;
            
            if (!ValidateRoutingNumber(routingNumber))
            {
                result.Errors.Add($"{fieldName} is not a valid routing number.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }

        private static bool ValidateRoutingNumber(string routingNumber)
        {
            if (string.IsNullOrWhiteSpace(routingNumber) || routingNumber.Length != 9)
                return false;

            if (!routingNumber.All(char.IsDigit))
                return false;

            return ValidateRoutingNumberChecksum(routingNumber);
        }

        private static bool ValidateRoutingNumberChecksum(string routingNumber)
        {
            var checksum = 0;
            for (int i = 0; i < 9; i++)
            {
                var digit = int.Parse(routingNumber[i].ToString());
                var weight = (i % 3) switch
                {
                    0 => 3,
                    1 => 7,
                    2 => 1,
                    _ => 0
                };
                checksum += digit * weight;
            }

            return checksum % 10 == 0;
        }
    }
}