using NoLock.Social.Core.OCR.Models;
using System.Text.RegularExpressions;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Factory for creating default field validation rules based on field type.
    /// </summary>
    public class FieldRulesFactory
    {
        private static readonly Dictionary<FieldType, string> ValidationPatterns = new()
        {
            [FieldType.Email] = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            [FieldType.Phone] = @"^(\+?1[-.\s]?)?\(?([0-9]{3})\)?[-.\s]?([0-9]{3})[-.\s]?([0-9]{4})$",
            [FieldType.RoutingNumber] = @"^[0-9]{9}$",
            [FieldType.AccountNumber] = @"^[0-9]{4,17}$",
            [FieldType.PostalCode] = @"^[0-9]{5}(-[0-9]{4})?$"
        };

        /// <summary>
        /// Creates default validation rules for a field.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <param name="documentType">The document type.</param>
        /// <returns>The default validation rules for the field.</returns>
        public FieldValidationRules CreateDefaultRules(string fieldName, string documentType)
        {
            var fieldType = DetermineFieldType(fieldName);
            var rules = new FieldValidationRules
            {
                FieldName = fieldName,
                FieldType = fieldType,
                IsRequired = DetermineIfRequired(fieldName, documentType)
            };

            ApplyTypeSpecificDefaults(rules, fieldType);
            return rules;
        }

        private static void ApplyTypeSpecificDefaults(FieldValidationRules rules, FieldType fieldType)
        {
            switch (fieldType)
            {
                case FieldType.Email:
                    SetEmailDefaults(rules);
                    break;
                
                case FieldType.Phone:
                    SetPhoneDefaults(rules);
                    break;
                
                case FieldType.Currency:
                    SetCurrencyDefaults(rules);
                    break;
                
                case FieldType.Percentage:
                    SetPercentageDefaults(rules);
                    break;
                
                case FieldType.Text:
                    SetTextDefaults(rules);
                    break;
            }
        }

        private static void SetEmailDefaults(FieldValidationRules rules)
        {
            rules.MaxLength = 254;
            rules.Pattern = ValidationPatterns[FieldType.Email];
        }

        private static void SetPhoneDefaults(FieldValidationRules rules)
        {
            rules.MaxLength = 20;
            rules.Pattern = ValidationPatterns[FieldType.Phone];
        }

        private static void SetCurrencyDefaults(FieldValidationRules rules)
        {
            rules.MinValue = 0;
            rules.MaxValue = 1_000_000;
        }

        private static void SetPercentageDefaults(FieldValidationRules rules)
        {
            rules.MinValue = 0;
            rules.MaxValue = 100;
        }

        private static void SetTextDefaults(FieldValidationRules rules)
        {
            rules.MaxLength = 255;
        }

        private static FieldType DetermineFieldType(string fieldName)
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

        private static bool DetermineIfRequired(string fieldName, string documentType)
        {
            var lowerName = fieldName.ToLower();
            
            // Common required fields
            if (lowerName.Contains("total") || lowerName.Contains("amount")) return true;
            if (lowerName.Contains("date")) return true;
            
            // Document-specific required fields
            return documentType.ToLower() switch
            {
                "receipt" => IsRequiredReceiptField(lowerName),
                "check" => IsRequiredCheckField(lowerName),
                "w4" => IsRequiredW4Field(lowerName),
                _ => false
            };
        }

        private static bool IsRequiredReceiptField(string lowerName)
        {
            return lowerName is "storename" or "total" or "transactiondate";
        }

        private static bool IsRequiredCheckField(string lowerName)
        {
            return lowerName is "amount" or "payto" or "date" or "routingnumber" or "accountnumber";
        }

        private static bool IsRequiredW4Field(string lowerName)
        {
            return lowerName is "name" or "ssn" or "address";
        }
    }
}