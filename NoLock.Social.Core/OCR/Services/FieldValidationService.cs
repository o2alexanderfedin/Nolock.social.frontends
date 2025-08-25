using System.Text.RegularExpressions;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Services
{
    /// <summary>
    /// Service for validating OCR field values based on field type and document context.
    /// Provides comprehensive validation rules for different data types and formats.
    /// </summary>
    public class FieldValidationService
    {
        private readonly Dictionary<string, FieldValidationRules> _fieldRulesCache = new();
        
        // Common validation patterns
        private static readonly Dictionary<FieldType, string> ValidationPatterns = new()
        {
            [FieldType.Email] = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            [FieldType.Phone] = @"^(\+?1[-.\s]?)?\(?([0-9]{3})\)?[-.\s]?([0-9]{3})[-.\s]?([0-9]{4})$",
            [FieldType.RoutingNumber] = @"^[0-9]{9}$",
            [FieldType.AccountNumber] = @"^[0-9]{4,17}$",
            [FieldType.PostalCode] = @"^[0-9]{5}(-[0-9]{4})?$"
        };

        /// <summary>
        /// Validates a field value against its type and document context.
        /// </summary>
        /// <param name="fieldName">The name of the field being validated.</param>
        /// <param name="value">The value to validate.</param>
        /// <param name="documentType">The type of document (Receipt, Check, etc.).</param>
        /// <param name="rules">Optional specific validation rules for the field.</param>
        /// <returns>A validation result with any errors or warnings.</returns>
        public async Task<FieldValidationResult> ValidateFieldAsync(
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
        public async Task<FieldValidationRules> GetFieldValidationRulesAsync(string fieldName, string documentType)
        {
            var cacheKey = $"{documentType}:{fieldName}";
            
            if (_fieldRulesCache.TryGetValue(cacheKey, out var cachedRules))
                return cachedRules;

            var rules = CreateDefaultRulesForField(fieldName, documentType);
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
            switch (fieldType)
            {
                case FieldType.Decimal:
                case FieldType.Currency:
                    await ValidateDecimalField(value, fieldName, result);
                    break;
                
                case FieldType.Integer:
                    await ValidateIntegerField(value, fieldName, result);
                    break;
                
                case FieldType.Date:
                case FieldType.DateTime:
                    await ValidateDateField(value, fieldName, result);
                    break;
                
                case FieldType.Email:
                    await ValidateEmailField(value, fieldName, result);
                    break;
                
                case FieldType.Phone:
                    await ValidatePhoneField(value, fieldName, result);
                    break;
                
                case FieldType.RoutingNumber:
                    await ValidateRoutingNumberField(value, fieldName, result);
                    break;
                
                case FieldType.AccountNumber:
                    await ValidateAccountNumberField(value, fieldName, result);
                    break;
                
                case FieldType.PostalCode:
                    await ValidatePostalCodeField(value, fieldName, result);
                    break;
                
                case FieldType.Percentage:
                    await ValidatePercentageField(value, fieldName, result);
                    break;
                
                case FieldType.Boolean:
                    await ValidateBooleanField(value, fieldName, result);
                    break;
                
                case FieldType.Text:
                default:
                    await ValidateTextField(value, fieldName, result);
                    break;
            }
        }

        private async Task ValidateDecimalField(object value, string fieldName, FieldValidationResult result)
        {
            if (!decimal.TryParse(value.ToString(), out var decimalValue))
            {
                result.Errors.Add($"{fieldName} must be a valid number.");
                result.IsValid = false;
                return;
            }

            if (fieldName.ToLower().Contains("amount") || fieldName.ToLower().Contains("total") || fieldName.ToLower().Contains("price"))
            {
                var currencyResult = ValidateCurrencyAmount(decimalValue, fieldName);
                result.Errors.AddRange(currencyResult.Errors);
                result.Warnings.AddRange(currencyResult.Warnings);
                if (!currencyResult.IsValid) result.IsValid = false;
            }

            await Task.CompletedTask;
        }

        private async Task ValidateIntegerField(object value, string fieldName, FieldValidationResult result)
        {
            if (!int.TryParse(value.ToString(), out var intValue))
            {
                result.Errors.Add($"{fieldName} must be a valid whole number.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }

        private async Task ValidateDateField(object value, string fieldName, FieldValidationResult result)
        {
            if (!DateTime.TryParse(value.ToString(), out var dateValue))
            {
                result.Errors.Add($"{fieldName} must be a valid date.");
                result.IsValid = false;
                return;
            }

            // Check for reasonable date ranges
            var now = DateTime.Now;
            if (dateValue > now.AddDays(1))
            {
                result.Warnings.Add($"{fieldName} is in the future. Please verify the date is correct.");
            }

            if (dateValue < now.AddYears(-10))
            {
                result.Warnings.Add($"{fieldName} is more than 10 years old. Please verify the date is correct.");
            }

            await Task.CompletedTask;
        }

        private async Task ValidateEmailField(object value, string fieldName, FieldValidationResult result)
        {
            var email = value.ToString()!;
            if (!Regex.IsMatch(email, ValidationPatterns[FieldType.Email], RegexOptions.IgnoreCase))
            {
                result.Errors.Add($"{fieldName} must be a valid email address.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }

        private async Task ValidatePhoneField(object value, string fieldName, FieldValidationResult result)
        {
            var phone = value.ToString()!;
            if (!Regex.IsMatch(phone, ValidationPatterns[FieldType.Phone]))
            {
                result.Errors.Add($"{fieldName} must be a valid phone number format.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }

        private async Task ValidateRoutingNumberField(object value, string fieldName, FieldValidationResult result)
        {
            var routingNumber = value.ToString()!;
            if (!ValidateRoutingNumber(routingNumber))
            {
                result.Errors.Add($"{fieldName} is not a valid routing number.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }

        private async Task ValidateAccountNumberField(object value, string fieldName, FieldValidationResult result)
        {
            var accountNumber = value.ToString()!;
            if (!Regex.IsMatch(accountNumber, ValidationPatterns[FieldType.AccountNumber]))
            {
                result.Errors.Add($"{fieldName} must be 4-17 digits.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }

        private async Task ValidatePostalCodeField(object value, string fieldName, FieldValidationResult result)
        {
            var postalCode = value.ToString()!;
            if (!Regex.IsMatch(postalCode, ValidationPatterns[FieldType.PostalCode]))
            {
                result.Errors.Add($"{fieldName} must be a valid ZIP code (e.g., 12345 or 12345-6789).");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }

        private async Task ValidatePercentageField(object value, string fieldName, FieldValidationResult result)
        {
            if (!decimal.TryParse(value.ToString(), out var percentage))
            {
                result.Errors.Add($"{fieldName} must be a valid percentage.");
                result.IsValid = false;
                return;
            }

            if (percentage < 0 || percentage > 100)
            {
                result.Errors.Add($"{fieldName} must be between 0 and 100.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }

        private async Task ValidateBooleanField(object value, string fieldName, FieldValidationResult result)
        {
            var stringValue = value.ToString()!.ToLower();
            if (!new[] { "true", "false", "yes", "no", "1", "0" }.Contains(stringValue))
            {
                result.Errors.Add($"{fieldName} must be true/false or yes/no.");
                result.IsValid = false;
            }

            await Task.CompletedTask;
        }

        private async Task ValidateTextField(object value, string fieldName, FieldValidationResult result)
        {
            var text = value.ToString()!;
            
            // Check for potentially dangerous content
            if (text.Contains('<') || text.Contains('>') || text.Contains("script"))
            {
                result.Warnings.Add($"{fieldName} contains special characters that may need review.");
            }

            await Task.CompletedTask;
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
            switch (documentType.ToLower())
            {
                case "receipt":
                    await ApplyReceiptSpecificRules(fieldName, value, result);
                    break;
                
                case "check":
                    await ApplyCheckSpecificRules(fieldName, value, result);
                    break;
                
                case "w4":
                    await ApplyW4SpecificRules(fieldName, value, result);
                    break;
            }
        }

        private async Task ApplyReceiptSpecificRules(string fieldName, object value, FieldValidationResult result)
        {
            switch (fieldName.ToLower())
            {
                case "total":
                case "subtotal":
                case "taxamount":
                    if (decimal.TryParse(value.ToString(), out var amount) && amount < 0)
                    {
                        result.Errors.Add($"{fieldName} cannot be negative on a receipt.");
                        result.IsValid = false;
                    }
                    break;
                
                case "transactiondate":
                    if (DateTime.TryParse(value.ToString(), out var date) && date > DateTime.Now)
                    {
                        result.Warnings.Add("Transaction date is in the future.");
                    }
                    break;
            }

            await Task.CompletedTask;
        }

        private async Task ApplyCheckSpecificRules(string fieldName, object value, FieldValidationResult result)
        {
            switch (fieldName.ToLower())
            {
                case "amount":
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
                    break;
                
                case "date":
                    if (DateTime.TryParse(value.ToString(), out var checkDate))
                    {
                        if (checkDate > DateTime.Now.AddDays(180))
                        {
                            result.Warnings.Add("Check is post-dated more than 6 months in the future.");
                        }
                    }
                    break;
            }

            await Task.CompletedTask;
        }

        private async Task ApplyW4SpecificRules(string fieldName, object value, FieldValidationResult result)
        {
            // W4-specific validation rules would go here
            await Task.CompletedTask;
        }

        private FieldValidationRules CreateDefaultRulesForField(string fieldName, string documentType)
        {
            var rules = new FieldValidationRules
            {
                FieldName = fieldName,
                FieldType = DetermineFieldType(fieldName),
                IsRequired = DetermineIfRequired(fieldName, documentType)
            };

            // Set type-specific defaults
            switch (rules.FieldType)
            {
                case FieldType.Email:
                    rules.MaxLength = 254;
                    rules.Pattern = ValidationPatterns[FieldType.Email];
                    break;
                
                case FieldType.Phone:
                    rules.MaxLength = 20;
                    rules.Pattern = ValidationPatterns[FieldType.Phone];
                    break;
                
                case FieldType.Currency:
                    rules.MinValue = 0;
                    rules.MaxValue = 1_000_000;
                    break;
                
                case FieldType.Percentage:
                    rules.MinValue = 0;
                    rules.MaxValue = 100;
                    break;
                
                case FieldType.Text:
                    rules.MaxLength = 255;
                    break;
            }

            return rules;
        }

        private FieldType DetermineFieldType(string fieldName)
        {
            var lowerName = fieldName.ToLower();
            
            if (lowerName.Contains("email")) return FieldType.Email;
            if (lowerName.Contains("phone") || lowerName.Contains("tel")) return FieldType.Phone;
            if (lowerName.Contains("amount") || lowerName.Contains("total") || lowerName.Contains("price") || lowerName.Contains("cost")) return FieldType.Currency;
            if (lowerName.Contains("date") || lowerName.Contains("time")) return FieldType.Date;
            if (lowerName.Contains("percent") || lowerName.Contains("rate")) return FieldType.Percentage;
            if (lowerName.Contains("routing")) return FieldType.RoutingNumber;
            if (lowerName.Contains("account") && lowerName.Contains("number")) return FieldType.AccountNumber;
            if (lowerName.Contains("zip") || lowerName.Contains("postal")) return FieldType.PostalCode;
            if (lowerName.Contains("quantity") || lowerName.Contains("count")) return FieldType.Integer;
            
            return FieldType.Text;
        }

        private bool DetermineIfRequired(string fieldName, string documentType)
        {
            var lowerName = fieldName.ToLower();
            
            // Common required fields
            if (lowerName.Contains("total") || lowerName.Contains("amount")) return true;
            if (lowerName.Contains("date")) return true;
            
            // Document-specific required fields
            return documentType.ToLower() switch
            {
                "receipt" => lowerName is "storename" or "total" or "transactiondate",
                "check" => lowerName is "amount" or "payto" or "date" or "routingnumber" or "accountnumber",
                "w4" => lowerName is "name" or "ssn" or "address",
                _ => false
            };
        }
    }
}