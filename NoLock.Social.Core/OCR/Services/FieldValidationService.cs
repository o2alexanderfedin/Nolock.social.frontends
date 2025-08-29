using System.Text.RegularExpressions;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services.Validation;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Service for validating OCR field values based on field type and document context.
    /// Provides comprehensive validation rules for different data types and formats.
    /// </summary>
    public class FieldValidationService
    {
        private readonly Dictionary<string, FieldValidationRules> _fieldRulesCache = new();
        private readonly FieldValidatorFactory _validatorFactory;
        private readonly DocumentSpecificRulesHandler _documentRulesHandler;
        private readonly FieldRulesFactory _fieldRulesFactory;
        
        /// <summary>
        /// Initializes a new instance of the FieldValidationService class.
        /// </summary>
        public FieldValidationService()
        {
            _validatorFactory = new FieldValidatorFactory();
            _documentRulesHandler = new DocumentSpecificRulesHandler();
            _fieldRulesFactory = new FieldRulesFactory();
        }


        /// <summary>
        /// Validates a field value against its type and document context.
        /// </summary>
        /// <param name="fieldName">The name of the field being validated.</param>
        /// <param name="value">The value to validate.</param>
        /// <param name="documentType">The type of document (Receipt, Check, etc.).</param>
        /// <param name="rules">Optional specific validation rules for the field.</param>
        /// <returns>A validation result with any errors or warnings.</returns>
        public virtual async Task<FieldValidationResult> ValidateFieldAsync(
            string fieldName, 
            object? value, 
            string documentType,
            FieldValidationRules? rules = null)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                return FieldValidationResult.Failure("Field name is required for validation.");

            rules ??= await GetFieldValidationRulesAsync(fieldName, documentType);
            var result = new FieldValidationResult();

            // Check if field is required
            if (rules.IsRequired && IsValueEmpty(value))
            {
                result.Errors.Add($"{fieldName} is required.");
                result.IsValid = false;
                return result;
            }

            // Skip further validation if value is empty and not required
            if (IsValueEmpty(value))
                return result;

            // Validate based on field type
            await ValidateByFieldType(value!, rules.FieldType, fieldName, result);

            // Apply field-specific validation rules
            await ApplyFieldSpecificRules(value!, rules, result);

            // Apply document-specific business rules
            await ApplyDocumentSpecificRules(fieldName, value!, documentType, result);

            return result;
        }

        /// <summary>
        /// Gets validation rules for a specific field and document type.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <param name="documentType">The document type.</param>
        /// <returns>The validation rules for the field.</returns>
        public virtual async Task<FieldValidationRules> GetFieldValidationRulesAsync(string fieldName, string documentType)
        {
            var cacheKey = $"{documentType}:{fieldName}";
            
            if (_fieldRulesCache.TryGetValue(cacheKey, out var cachedRules))
                return cachedRules;

            var rules = _fieldRulesFactory.CreateDefaultRules(fieldName, documentType);
            _fieldRulesCache[cacheKey] = rules;
            
            return await Task.FromResult(rules);
        }

        /// <summary>
        /// Validates a routing number using the standard checksum algorithm.
        /// </summary>
        /// <param name="routingNumber">The routing number to validate.</param>
        /// <returns>True if the routing number is valid; otherwise, false.</returns>
        public bool ValidateRoutingNumber(string routingNumber)
        {
            if (string.IsNullOrWhiteSpace(routingNumber) || routingNumber.Length != 9)
                return false;

            if (!routingNumber.All(char.IsDigit))
                return false;

            // Calculate checksum using the standard algorithm
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

        /// <summary>
        /// Validates currency amounts for reasonable ranges and formatting.
        /// </summary>
        /// <param name="amount">The amount to validate.</param>
        /// <param name="fieldName">The field name for context.</param>
        /// <returns>A validation result.</returns>
        public FieldValidationResult ValidateCurrencyAmount(decimal amount, string fieldName)
        {
            var result = new FieldValidationResult();

            if (amount < 0)
            {
                result.Errors.Add($"{fieldName} cannot be negative.");
                result.IsValid = false;
            }

            if (amount > 1_000_000)
            {
                result.Warnings.Add($"{fieldName} amount ${amount:N2} is unusually large. Please verify.");
            }

            // Check for unrealistic precision (more than 2 decimal places for currency)
            if (Math.Round(amount, 2) != amount)
            {
                result.Warnings.Add($"{fieldName} has more than 2 decimal places. Currency amounts typically use 2 decimal places.");
            }

            return result;
        }

        private static bool IsValueEmpty(object? value)
        {
            return value == null || 
                   (value is string str && string.IsNullOrWhiteSpace(str)) ||
                   (value is DateTime dt && dt == default);
        }

        private async Task ValidateByFieldType(object value, FieldType fieldType, string fieldName, FieldValidationResult result)
        {
            var validator = _validatorFactory.GetValidator(fieldType);
            await validator.ValidateAsync(value, fieldName, result);
        }


        private async Task ApplyFieldSpecificRules(object value, FieldValidationRules rules, FieldValidationResult result)
        {
            var stringValue = value.ToString()!;

            // Length validation
            if (rules.MinLength.HasValue && stringValue.Length < rules.MinLength.Value)
            {
                result.Errors.Add($"Field must be at least {rules.MinLength.Value} characters long.");
                result.IsValid = false;
            }

            if (rules.MaxLength.HasValue && stringValue.Length > rules.MaxLength.Value)
            {
                result.Errors.Add($"Field cannot exceed {rules.MaxLength.Value} characters.");
                result.IsValid = false;
            }

            // Numeric range validation
            if (decimal.TryParse(stringValue, out var numericValue))
            {
                if (rules.MinValue.HasValue && numericValue < rules.MinValue.Value)
                {
                    result.Errors.Add($"Value must be at least {rules.MinValue.Value}.");
                    result.IsValid = false;
                }

                if (rules.MaxValue.HasValue && numericValue > rules.MaxValue.Value)
                {
                    result.Errors.Add($"Value cannot exceed {rules.MaxValue.Value}.");
                    result.IsValid = false;
                }
            }

            // Pattern validation
            if (!string.IsNullOrEmpty(rules.Pattern) && !Regex.IsMatch(stringValue, rules.Pattern))
            {
                var message = !string.IsNullOrEmpty(rules.ValidationMessage) 
                    ? rules.ValidationMessage 
                    : $"Field does not match the required format.";
                result.Errors.Add(message);
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }

        private async Task ApplyDocumentSpecificRules(string fieldName, object value, string documentType, FieldValidationResult result)
        {
            await _documentRulesHandler.ApplyRulesAsync(fieldName, value, documentType, result);
        }

    }
}