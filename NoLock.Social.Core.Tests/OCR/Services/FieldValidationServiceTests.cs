using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using NoLock.Social.Core.OCR.Services;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.Tests.OCR.Services
{
    /// <summary>
    /// Comprehensive unit tests for FieldValidationService with extensive coverage.
    /// Uses data-driven testing to validate field validation logic across different scenarios.
    /// </summary>
    public class FieldValidationServiceTests
    {
        private readonly FieldValidationService _validationService;

        public FieldValidationServiceTests()
        {
            _validationService = new FieldValidationService();
        }

        #region ValidateFieldAsync Tests

        [Fact]
        public async Task ValidateFieldAsync_EmptyFieldName_ReturnsFailure()
        {
            // Act
            var result = await _validationService.ValidateFieldAsync("", "test@email.com", "Receipt");

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain("Field name is required for validation.");
        }

        [Theory]
        [InlineData(null, "null field name")]
        [InlineData("", "empty field name")]
        [InlineData("   ", "whitespace field name")]
        public async Task ValidateFieldAsync_InvalidFieldName_ReturnsFailure(string fieldName, string scenario)
        {
            // Act
            var result = await _validationService.ValidateFieldAsync(fieldName, "value", "Receipt");

            // Assert
            result.Should().NotBeNull($"Failed for {scenario}");
            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public async Task ValidateFieldAsync_RequiredFieldWithNullValue_ReturnsError()
        {
            // Arrange
            var rules = new FieldValidationRules
            {
                FieldName = "Total",
                IsRequired = true,
                FieldType = FieldType.Currency
            };

            // Act
            var result = await _validationService.ValidateFieldAsync("Total", null, "Receipt", rules);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain("Total is required.");
        }

        [Theory]
        [InlineData("", "empty string")]
        [InlineData("   ", "whitespace")]
        public async Task ValidateFieldAsync_RequiredFieldWithEmptyValue_ReturnsError(string value, string scenario)
        {
            // Arrange
            var rules = new FieldValidationRules
            {
                FieldName = "StoreName",
                IsRequired = true,
                FieldType = FieldType.Text
            };

            // Act
            var result = await _validationService.ValidateFieldAsync("StoreName", value, "Receipt", rules);

            // Assert
            result.IsValid.Should().BeFalse($"Failed for {scenario}");
            result.Errors.Should().Contain("StoreName is required.");
        }

        [Fact]
        public async Task ValidateFieldAsync_NonRequiredEmptyField_ReturnsValid()
        {
            // Arrange
            var rules = new FieldValidationRules
            {
                FieldName = "OptionalField",
                IsRequired = false,
                FieldType = FieldType.Text
            };

            // Act
            var result = await _validationService.ValidateFieldAsync("OptionalField", "", "Receipt", rules);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        #endregion

        #region Email Validation Tests

        [Theory]
        [InlineData("test@example.com", true, "valid email")]
        [InlineData("user.name@domain.co.uk", true, "email with dots")]
        [InlineData("user+tag@example.com", true, "email with plus")]
        [InlineData("invalid.email", false, "missing @")]
        [InlineData("@example.com", false, "missing local part")]
        [InlineData("test@", false, "missing domain")]
        [InlineData("test@@example.com", false, "double @")]
        public async Task ValidateFieldAsync_EmailField_ValidatesCorrectly(
            string email, bool expectedValid, string scenario)
        {
            // Act
            var result = await _validationService.ValidateFieldAsync("Email", email, "Receipt");

            // Assert
            result.IsValid.Should().Be(expectedValid, $"Failed for {scenario}");
            if (!expectedValid)
            {
                result.Errors.Should().Contain(e => e.Contains("valid email address"));
            }
        }

        #endregion

        #region Phone Number Validation Tests

        [Theory]
        [InlineData("(555) 123-4567", true, "formatted phone")]
        [InlineData("555-123-4567", true, "dashed phone")]
        [InlineData("5551234567", true, "plain phone")]
        [InlineData("+1-555-123-4567", true, "international phone")]
        [InlineData("123", false, "too short")]
        [InlineData("abc-def-ghij", false, "letters")]
        public async Task ValidateFieldAsync_PhoneField_ValidatesCorrectly(
            string phone, bool expectedValid, string scenario)
        {
            // Act
            var result = await _validationService.ValidateFieldAsync("Phone", phone, "Receipt");

            // Assert
            result.IsValid.Should().Be(expectedValid, $"Failed for {scenario}");
            if (!expectedValid)
            {
                result.Errors.Should().Contain(e => e.Contains("valid phone number"));
            }
        }

        #endregion

        #region Currency Validation Tests

        [Theory]
        [InlineData("100.00", true, false, "valid amount")]
        [InlineData("0", true, false, "zero amount")]
        [InlineData("-50.00", false, false, "negative amount")]
        [InlineData("1000001", true, true, "very large amount")]
        [InlineData("abc", false, false, "non-numeric")]
        [InlineData("12.345", true, true, "too many decimals")]
        public async Task ValidateCurrencyAmount_ValidatesCorrectly(
            string amount, bool expectedValid, bool expectWarning, string scenario)
        {
            // Arrange
            if (!decimal.TryParse(amount, out var decimalAmount))
            {
                // For invalid numeric values, test through ValidateFieldAsync
                var fieldResult = await _validationService.ValidateFieldAsync("Amount", amount, "Receipt");
                fieldResult.IsValid.Should().BeFalse($"Failed for {scenario}");
                return;
            }

            // Act
            var result = _validationService.ValidateCurrencyAmount(decimalAmount, "Amount");

            // Assert
            result.IsValid.Should().Be(expectedValid, $"Failed for {scenario}");
            if (expectWarning)
            {
                result.Warnings.Should().NotBeEmpty();
            }
        }

        #endregion

        #region Routing Number Validation Tests

        [Theory]
        [InlineData("121000248", true, "valid routing number")]
        [InlineData("021000021", true, "another valid routing")]
        [InlineData("123456789", false, "invalid checksum")]
        [InlineData("12345678", false, "too short")]
        [InlineData("1234567890", false, "too long")]
        [InlineData("12345678a", false, "contains letter")]
        public void ValidateRoutingNumber_ValidatesCorrectly(
            string routingNumber, bool expectedValid, string scenario)
        {
            // Act
            var result = _validationService.ValidateRoutingNumber(routingNumber);

            // Assert
            result.Should().Be(expectedValid, $"Failed for {scenario}");
        }

        [Fact]
        public async Task ValidateFieldAsync_RoutingNumberField_UsesChecksumValidation()
        {
            // Arrange
            var validRouting = "121000248";
            var invalidRouting = "123456789";

            // Act
            var validResult = await _validationService.ValidateFieldAsync("RoutingNumber", validRouting, "Check");
            var invalidResult = await _validationService.ValidateFieldAsync("RoutingNumber", invalidRouting, "Check");

            // Assert
            validResult.IsValid.Should().BeTrue();
            invalidResult.IsValid.Should().BeFalse();
            invalidResult.Errors.Should().Contain(e => e.Contains("valid routing number"));
        }

        #endregion

        #region Date Validation Tests

        [Theory]
        [InlineData("2024-01-15", true, false, "valid date")]
        [InlineData("2050-01-01", true, true, "future date")]
        [InlineData("1900-01-01", true, true, "very old date")]
        [InlineData("not-a-date", false, false, "invalid format")]
        [InlineData("2024-13-01", false, false, "invalid month")]
        public async Task ValidateFieldAsync_DateField_ValidatesCorrectly(
            string date, bool expectedValid, bool expectWarning, string scenario)
        {
            // Act
            var result = await _validationService.ValidateFieldAsync("TransactionDate", date, "Receipt");

            // Assert
            if (!DateTime.TryParse(date, out _))
            {
                result.IsValid.Should().BeFalse($"Failed for {scenario}");
                result.Errors.Should().Contain(e => e.Contains("valid date"));
            }
            else
            {
                result.IsValid.Should().Be(expectedValid, $"Failed for {scenario}");
                if (expectWarning)
                {
                    result.Warnings.Should().NotBeEmpty();
                }
            }
        }

        #endregion

        #region Postal Code Validation Tests

        [Theory]
        [InlineData("12345", true, "5-digit zip")]
        [InlineData("12345-6789", true, "zip+4")]
        [InlineData("1234", false, "too short")]
        [InlineData("123456", false, "6 digits")]
        [InlineData("ABCDE", false, "letters")]
        public async Task ValidateFieldAsync_PostalCodeField_ValidatesCorrectly(
            string postalCode, bool expectedValid, string scenario)
        {
            // Act
            var result = await _validationService.ValidateFieldAsync("PostalCode", postalCode, "Receipt");

            // Assert
            result.IsValid.Should().Be(expectedValid, $"Failed for {scenario}");
            if (!expectedValid)
            {
                result.Errors.Should().Contain(e => e.Contains("ZIP code"));
            }
        }

        #endregion

        #region Account Number Validation Tests

        [Theory]
        [InlineData("1234", true, "minimum length")]
        [InlineData("12345678901234567", true, "maximum length")]
        [InlineData("123", false, "too short")]
        [InlineData("123456789012345678", false, "too long")]
        [InlineData("12AB5678", false, "contains letters")]
        public async Task ValidateFieldAsync_AccountNumberField_ValidatesCorrectly(
            string accountNumber, bool expectedValid, string scenario)
        {
            // Act
            var result = await _validationService.ValidateFieldAsync("AccountNumber", accountNumber, "Check");

            // Assert
            result.IsValid.Should().Be(expectedValid, $"Failed for {scenario}");
            if (!expectedValid)
            {
                result.Errors.Should().Contain(e => e.Contains("4-17 digits"));
            }
        }

        #endregion

        #region Percentage Validation Tests

        [Theory]
        [InlineData("0", true, "zero percent")]
        [InlineData("50", true, "mid range")]
        [InlineData("100", true, "maximum")]
        [InlineData("-10", false, "negative")]
        [InlineData("150", false, "over 100")]
        [InlineData("abc", false, "non-numeric")]
        public async Task ValidateFieldAsync_PercentageField_ValidatesCorrectly(
            string percentage, bool expectedValid, string scenario)
        {
            // Act
            var result = await _validationService.ValidateFieldAsync("TaxRate", percentage, "Receipt");

            // Assert
            if (!decimal.TryParse(percentage, out _))
            {
                result.IsValid.Should().BeFalse($"Failed for {scenario}");
                result.Errors.Should().Contain(e => e.Contains("valid percentage"));
            }
            else
            {
                result.IsValid.Should().Be(expectedValid, $"Failed for {scenario}");
                if (!expectedValid)
                {
                    result.Errors.Should().Contain(e => e.Contains("between 0 and 100"));
                }
            }
        }

        #endregion

        #region Boolean Validation Tests

        [Theory]
        [InlineData("true", true, "true value")]
        [InlineData("false", true, "false value")]
        [InlineData("yes", true, "yes value")]
        [InlineData("no", true, "no value")]
        [InlineData("1", true, "numeric true")]
        [InlineData("0", true, "numeric false")]
        [InlineData("maybe", false, "invalid value")]
        public async Task ValidateFieldAsync_BooleanField_ValidatesCorrectly(
            string value, bool expectedValid, string scenario)
        {
            // Arrange - Create a boolean field validation
            var rules = new FieldValidationRules
            {
                FieldName = "IsActive",
                FieldType = FieldType.Boolean
            };

            // Act
            var result = await _validationService.ValidateFieldAsync("IsActive", value, "Receipt", rules);

            // Assert
            result.IsValid.Should().Be(expectedValid, $"Failed for {scenario}");
            if (!expectedValid)
            {
                result.Errors.Should().Contain(e => e.Contains("true/false or yes/no"));
            }
        }

        #endregion

        #region Field-Specific Rules Tests

        [Theory]
        [InlineData("ab", 3, 10, false, "below minimum length")]
        [InlineData("test", 3, 10, true, "within range")]
        [InlineData("verylongstring", 3, 10, false, "exceeds maximum length")]
        public async Task ValidateFieldAsync_WithLengthRules_ValidatesCorrectly(
            string value, int minLength, int maxLength, bool expectedValid, string scenario)
        {
            // Arrange
            var rules = new FieldValidationRules
            {
                FieldName = "TestField",
                FieldType = FieldType.Text,
                MinLength = minLength,
                MaxLength = maxLength
            };

            // Act
            var result = await _validationService.ValidateFieldAsync("TestField", value, "Receipt", rules);

            // Assert
            result.IsValid.Should().Be(expectedValid, $"Failed for {scenario}");
        }

        [Theory]
        [InlineData("5", 1, 10, true, "within numeric range")]
        [InlineData("0", 1, 10, false, "below minimum")]
        [InlineData("15", 1, 10, false, "above maximum")]
        public async Task ValidateFieldAsync_WithNumericRules_ValidatesCorrectly(
            string value, decimal minValue, decimal maxValue, bool expectedValid, string scenario)
        {
            // Arrange
            var rules = new FieldValidationRules
            {
                FieldName = "Quantity",
                FieldType = FieldType.Integer,
                MinValue = minValue,
                MaxValue = maxValue
            };

            // Act
            var result = await _validationService.ValidateFieldAsync("Quantity", value, "Receipt", rules);

            // Assert
            result.IsValid.Should().Be(expectedValid, $"Failed for {scenario}");
        }

        [Fact]
        public async Task ValidateFieldAsync_WithPatternRule_ValidatesCorrectly()
        {
            // Arrange
            var rules = new FieldValidationRules
            {
                FieldName = "Code",
                FieldType = FieldType.Text,
                Pattern = @"^[A-Z]{3}-\d{3}$",
                ValidationMessage = "Code must be in format ABC-123"
            };

            // Act
            var validResult = await _validationService.ValidateFieldAsync("Code", "ABC-123", "Receipt", rules);
            var invalidResult = await _validationService.ValidateFieldAsync("Code", "abc-123", "Receipt", rules);

            // Assert
            validResult.IsValid.Should().BeTrue();
            invalidResult.IsValid.Should().BeFalse();
            invalidResult.Errors.Should().Contain("Code must be in format ABC-123");
        }

        #endregion

        #region Document-Specific Rules Tests

        [Theory]
        [InlineData("Receipt", "Total", "-10.00", false, "negative total on receipt")]
        [InlineData("Receipt", "Total", "50.00", true, "valid receipt total")]
        [InlineData("Check", "Amount", "0", false, "zero amount on check")]
        [InlineData("Check", "Amount", "100.00", true, "valid check amount")]
        [InlineData("Check", "Amount", "15000", true, "large check with warning")]
        public async Task ValidateFieldAsync_DocumentSpecificRules_AppliedCorrectly(
            string documentType, string fieldName, string value, bool expectedValid, string scenario)
        {
            // Act
            var result = await _validationService.ValidateFieldAsync(fieldName, value, documentType);

            // Assert
            result.IsValid.Should().Be(expectedValid, $"Failed for {scenario}");
            
            // Check for warnings on large amounts
            if (documentType == "Check" && decimal.TryParse(value, out var amount) && amount > 10000)
            {
                result.Warnings.Should().NotBeEmpty();
            }
        }

        #endregion

        #region GetFieldValidationRulesAsync Tests

        [Theory]
        [InlineData("Email", FieldType.Email, "email field detection")]
        [InlineData("PhoneNumber", FieldType.Phone, "phone field detection")]
        [InlineData("TotalAmount", FieldType.Currency, "currency field detection")]
        [InlineData("TransactionDate", FieldType.Date, "date field detection")]
        [InlineData("TaxPercentage", FieldType.Percentage, "percentage field detection")]
        [InlineData("RoutingNumber", FieldType.RoutingNumber, "routing field detection")]
        [InlineData("AccountNumber", FieldType.AccountNumber, "account field detection")]
        [InlineData("PostalCode", FieldType.PostalCode, "postal field detection")]
        [InlineData("Quantity", FieldType.Integer, "integer field detection")]
        [InlineData("Description", FieldType.Text, "text field default")]
        public async Task GetFieldValidationRulesAsync_DeterminesCorrectFieldType(
            string fieldName, FieldType expectedType, string scenario)
        {
            // Act
            var rules = await _validationService.GetFieldValidationRulesAsync(fieldName, "Receipt");

            // Assert
            rules.Should().NotBeNull();
            rules.FieldType.Should().Be(expectedType, $"Failed for {scenario}");
        }

        [Theory]
        [InlineData("Receipt", "Total", true, "receipt total required")]
        [InlineData("Receipt", "StoreName", true, "receipt store required")]
        [InlineData("Receipt", "Notes", false, "receipt notes optional")]
        [InlineData("Check", "Amount", true, "check amount required")]
        [InlineData("Check", "PayTo", true, "check payee required")]
        [InlineData("W4", "Name", true, "W4 name required")]
        [InlineData("W4", "SSN", true, "W4 SSN required")]
        public async Task GetFieldValidationRulesAsync_DeterminesRequiredFields(
            string documentType, string fieldName, bool expectedRequired, string scenario)
        {
            // Act
            var rules = await _validationService.GetFieldValidationRulesAsync(fieldName, documentType);

            // Assert
            rules.IsRequired.Should().Be(expectedRequired, $"Failed for {scenario}");
        }

        #endregion

        #region Text Field Special Character Tests

        [Theory]
        [InlineData("<script>alert('xss')</script>", true, "script tags")]
        [InlineData("Normal text", false, "clean text")]
        [InlineData("Price < 100", true, "less than symbol")]
        [InlineData("A > B", true, "greater than symbol")]
        public async Task ValidateFieldAsync_TextField_ChecksForSpecialCharacters(
            string text, bool expectWarning, string scenario)
        {
            // Act
            var result = await _validationService.ValidateFieldAsync("Description", text, "Receipt");

            // Assert
            result.IsValid.Should().BeTrue();
            if (expectWarning)
            {
                result.Warnings.Should().Contain(w => w.Contains("special characters"), $"Failed for {scenario}");
            }
            else
            {
                result.Warnings.Should().BeEmpty();
            }
        }

        #endregion

        #region Integer Validation Tests

        [Theory]
        [InlineData("42", true, "valid integer")]
        [InlineData("0", true, "zero")]
        [InlineData("-5", true, "negative integer")]
        [InlineData("3.14", false, "decimal number")]
        [InlineData("abc", false, "non-numeric")]
        public async Task ValidateFieldAsync_IntegerField_ValidatesCorrectly(
            string value, bool expectedValid, string scenario)
        {
            // Arrange
            var rules = new FieldValidationRules
            {
                FieldName = "Quantity",
                FieldType = FieldType.Integer
            };

            // Act
            var result = await _validationService.ValidateFieldAsync("Quantity", value, "Receipt", rules);

            // Assert
            result.IsValid.Should().Be(expectedValid, $"Failed for {scenario}");
            if (!expectedValid)
            {
                result.Errors.Should().Contain(e => e.Contains("whole number"));
            }
        }

        #endregion

        #region Decimal Validation Tests

        [Theory]
        [InlineData("123.45", true, "valid decimal")]
        [InlineData("0", true, "zero")]
        [InlineData("-99.99", true, "negative decimal")]
        [InlineData("not-a-number", false, "non-numeric")]
        public async Task ValidateFieldAsync_DecimalField_ValidatesCorrectly(
            string value, bool expectedValid, string scenario)
        {
            // Arrange
            var rules = new FieldValidationRules
            {
                FieldName = "Quantity",
                FieldType = FieldType.Decimal
            };

            // Act
            var result = await _validationService.ValidateFieldAsync("Quantity", value, "Receipt", rules);

            // Assert
            if (!decimal.TryParse(value, out _))
            {
                result.IsValid.Should().BeFalse($"Failed for {scenario}");
                result.Errors.Should().Contain(e => e.Contains("valid number"));
            }
            else
            {
                result.IsValid.Should().BeTrue($"Failed for {scenario}");
            }
        }

        #endregion

        #region Cache Tests

        [Fact]
        public async Task GetFieldValidationRulesAsync_CachesRules()
        {
            // Act - Call twice with same parameters
            var rules1 = await _validationService.GetFieldValidationRulesAsync("Total", "Receipt");
            var rules2 = await _validationService.GetFieldValidationRulesAsync("Total", "Receipt");

            // Assert - Should return same instance (cached)
            rules1.Should().BeSameAs(rules2);
        }

        [Fact]
        public async Task GetFieldValidationRulesAsync_DifferentFieldsGetDifferentRules()
        {
            // Act
            var rules1 = await _validationService.GetFieldValidationRulesAsync("Total", "Receipt");
            var rules2 = await _validationService.GetFieldValidationRulesAsync("Email", "Receipt");

            // Assert
            rules1.Should().NotBeSameAs(rules2);
            rules1.FieldType.Should().Be(FieldType.Currency);
            rules2.FieldType.Should().Be(FieldType.Email);
        }

        #endregion

        #region Edge Cases and Error Scenarios

        [Fact]
        public async Task ValidateFieldAsync_VeryLongString_HandlesGracefully()
        {
            // Arrange
            var longString = new string('a', 10000);

            // Act
            var result = await _validationService.ValidateFieldAsync("Notes", longString, "Receipt");

            // Assert
            result.Should().NotBeNull();
            result.Errors.Should().Contain(e => e.Contains("exceed"));
        }

        [Fact]
        public async Task ValidateFieldAsync_SpecialUnicodeCharacters_HandlesCorrectly()
        {
            // Arrange
            var unicodeText = "Test with emoji 😀 and special chars: ñ, ü, 中文";

            // Act
            var result = await _validationService.ValidateFieldAsync("Description", unicodeText, "Receipt");

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateRoutingNumber_NullOrEmpty_ReturnsFalse()
        {
            // Act & Assert
            _validationService.ValidateRoutingNumber(null).Should().BeFalse();
            _validationService.ValidateRoutingNumber("").Should().BeFalse();
            _validationService.ValidateRoutingNumber("   ").Should().BeFalse();
        }

        #endregion
    }
}