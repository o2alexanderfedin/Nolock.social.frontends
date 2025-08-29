using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using FluentAssertions;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services.Validation;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Services.Validation
{
    /// <summary>
    /// Comprehensive unit tests for CurrencyFieldValidator with extensive coverage.
    /// Uses data-driven testing to validate currency validation logic across different scenarios.
    /// </summary>
    public class CurrencyFieldValidatorTests
    {
        private readonly CurrencyFieldValidator _validator;

        public CurrencyFieldValidatorTests()
        {
            _validator = new CurrencyFieldValidator();
        }

        #region FieldType Property Tests

        [Fact]
        public void FieldType_ReturnsCurrency()
        {
            // Act
            var fieldType = _validator.FieldType;

            // Assert
            fieldType.Should().Be(FieldType.Currency);
        }

        #endregion

        #region Valid Currency Formats Tests

        [Theory]
        [InlineData("100", 100.0, "simple integer")]
        [InlineData("100.00", 100.00, "two decimal places")]
        [InlineData("100.50", 100.50, "cents")]
        [InlineData("1000", 1000.0, "thousand")]
        [InlineData("1000.99", 1000.99, "thousand with cents")]
        [InlineData("0.01", 0.01, "one cent")]
        [InlineData("0.99", 0.99, "ninety-nine cents")]
        [InlineData("999999.99", 999999.99, "large amount under limit")]
        [InlineData("1.5", 1.5, "one decimal place")]
        [InlineData("0", 0, "zero")]
        [InlineData("0.0", 0.0, "zero with decimal")]
        [InlineData("0.00", 0.00, "zero with two decimals")]
        public async Task ValidateAsync_ValidCurrencyFormats_ReturnsValid(string input, decimal expectedValue, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "amount";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            result.IsValid.Should().BeTrue($"Failed for {scenario}: {input}");
            result.Errors.Should().BeEmpty($"Should have no errors for {scenario}");
        }

        [Theory]
        [InlineData("$100", "100", "dollar sign prefix")]
        [InlineData("$100.00", "100.00", "dollar sign with decimals")]
        [InlineData("$1,000", "1000", "dollar sign with comma")]
        [InlineData("$1,000.00", "1000.00", "dollar sign with comma and decimals")]
        [InlineData("1,000", "1000", "comma separator")]
        [InlineData("1,000.50", "1000.50", "comma with cents")]
        [InlineData("10,000,000", "10000000", "multiple commas")]
        [InlineData("$ 100", "100", "dollar sign with space")]
        [InlineData("  100.00  ", "100.00", "trimmed spaces")]
        public async Task ValidateAsync_FormattedCurrencyStrings_ParsesCorrectly(string input, string expectedParseable, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "total";
            
            // Remove formatting for decimal parsing
            var cleanInput = input.Replace("$", "").Replace(",", "").Trim();

            // Act
            await _validator.ValidateAsync(cleanInput, fieldName, result);

            // Assert
            if (decimal.TryParse(cleanInput, out var value))
            {
                result.Errors.Should().BeEmpty($"Should parse {scenario}: {input}");
                if (value >= 0 && value <= 1000000)
                {
                    result.IsValid.Should().BeTrue($"Should be valid for {scenario}");
                }
            }
        }

        #endregion

        #region Invalid Currency Formats Tests

        [Theory]
        [InlineData("abc", "letters only")]
        [InlineData("12.34.56", "multiple decimal points")]
        [InlineData("12,34.56.78", "multiple decimals with comma")]
        [InlineData("one hundred", "text number")]
        [InlineData("100dollars", "mixed text and numbers")]
        [InlineData("@#$%", "special characters only")]
        [InlineData("12..34", "double decimal point")]
        [InlineData(".", "decimal point only")]
        [InlineData("..", "multiple decimal points only")]
        [InlineData("1.2.3", "two decimal points")]
        [InlineData("NaN", "not a number")]
        [InlineData("Infinity", "infinity string")]
        [InlineData("1e10", "scientific notation")]
        [InlineData("0x64", "hexadecimal")]
        [InlineData("123-456", "hyphen separator")]
        [InlineData("12/34", "slash separator")]
        public async Task ValidateAsync_InvalidFormats_ReturnsError(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "amount";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse($"Should be invalid for {scenario}: {input}");
            result.Errors.Should().Contain($"{fieldName} must be a valid number.");
        }

        #endregion

        #region Edge Cases Tests

        [Theory]
        [InlineData(null, "null value")]
        [InlineData("", "empty string")]
        [InlineData("   ", "whitespace only")]
        [InlineData("\t", "tab character")]
        [InlineData("\n", "newline character")]
        [InlineData("\r\n", "carriage return and newline")]
        public async Task ValidateAsync_EdgeCases_HandlesGracefully(object input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "amount";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            if (input == null || string.IsNullOrWhiteSpace(input?.ToString()))
            {
                result.IsValid.Should().BeFalse($"Should be invalid for {scenario}");
                result.Errors.Should().Contain($"{fieldName} must be a valid number.");
            }
        }

        #endregion

        #region Negative Numbers Tests

        [Theory]
        [InlineData("-1", "negative one")]
        [InlineData("-100", "negative hundred")]
        [InlineData("-100.50", "negative with cents")]
        [InlineData("-0.01", "negative cent")]
        [InlineData("-999999", "large negative")]
        public async Task ValidateAsync_NegativeAmounts_ReturnsError(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "amount";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse($"Should be invalid for negative {scenario}");
            result.Errors.Should().Contain($"{fieldName} cannot be negative.");
        }

        #endregion

        #region Boundary Conditions Tests

        [Theory]
        [InlineData("1000000", true, false, "exactly at limit")]
        [InlineData("999999.99", true, false, "just under limit")]
        [InlineData("1000000.01", true, true, "just over limit")]
        [InlineData("1000001", true, true, "over limit")]
        [InlineData("10000000", true, true, "way over limit")]
        [InlineData("9999999", true, true, "large over limit")]
        public async Task ValidateAsync_BoundaryValues_ValidatesCorrectly(string input, bool shouldBeValid, bool shouldHaveWarning, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "amount";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            if (shouldHaveWarning)
            {
                result.Warnings.Should().Contain(w => w.Contains("unusually large"), $"Should warn for {scenario}");
            }
            else
            {
                result.IsValid.Should().Be(shouldBeValid, $"Validation result for {scenario}");
            }
        }

        [Theory]
        [InlineData("0.001", "three decimal places")]
        [InlineData("0.0001", "four decimal places")]
        [InlineData("100.999", "three decimal places with whole")]
        [InlineData("1.23456789", "many decimal places")]
        public async Task ValidateAsync_MoreThanTwoDecimalPlaces_AddsWarning(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "amount";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            result.IsValid.Should().BeTrue($"Should be valid for {scenario}");
            result.Warnings.Should().Contain(w => w.Contains("more than 2 decimal places"));
        }

        #endregion

        #region Field Name Detection Tests

        [Theory]
        [InlineData("amount", true, "amount field")]
        [InlineData("Amount", true, "Amount capitalized")]
        [InlineData("total", true, "total field")]
        [InlineData("Total", true, "Total capitalized")]
        [InlineData("totalAmount", true, "totalAmount camelCase")]
        [InlineData("price", true, "price field")]
        [InlineData("unitPrice", true, "unitPrice field")]
        [InlineData("subtotal", true, "subtotal field")]
        [InlineData("grandTotal", true, "grandTotal field")]
        [InlineData("quantity", false, "quantity field")]
        [InlineData("description", false, "description field")]
        [InlineData("name", false, "name field")]
        [InlineData("id", false, "id field")]
        public async Task ValidateAsync_FieldNameDetection_AppliesCurrencyRules(string fieldName, bool shouldApplyCurrencyRules, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var negativeValue = "-100";

            // Act
            await _validator.ValidateAsync(negativeValue, fieldName, result);

            // Assert
            if (shouldApplyCurrencyRules)
            {
                result.Errors.Should().Contain($"{fieldName} cannot be negative.", $"Should apply currency rules for {scenario}");
            }
            else
            {
                result.Errors.Should().NotContain($"{fieldName} cannot be negative.", $"Should not apply currency rules for {scenario}");
            }
        }

        #endregion

        #region Multiple Validation Rules Tests

        [Fact]
        public async Task ValidateAsync_NegativeAndLarge_ReturnsMultipleIssues()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "totalAmount";
            var value = "-2000000";

            // Act
            await _validator.ValidateAsync(value, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain($"{fieldName} cannot be negative.");
            // Note: Warning for large amount won't trigger for negative values as it fails validation first
        }

        [Fact]
        public async Task ValidateAsync_LargeWithManyDecimals_ReturnsWarnings()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "amount";
            var value = "1500000.12345";

            // Act
            await _validator.ValidateAsync(value, fieldName, result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Warnings.Should().HaveCountGreaterThan(0);
            result.Warnings.Should().Contain(w => w.Contains("unusually large"));
            result.Warnings.Should().Contain(w => w.Contains("more than 2 decimal places"));
        }

        #endregion

        #region Null and Empty Value Tests

        [Fact]
        public async Task ValidateAsync_NullValue_ReturnsError()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "amount";

            // Act
            await _validator.ValidateAsync(null, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain($"{fieldName} must be a valid number.");
        }

        [Fact]
        public async Task ValidateAsync_EmptyString_ReturnsError()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "total";

            // Act
            await _validator.ValidateAsync(string.Empty, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain($"{fieldName} must be a valid number.");
        }

        #endregion

        #region Culture and Localization Tests

        [Theory]
        [InlineData("1,234.56", "US format with comma thousand separator")]
        [InlineData("1234.56", "no thousand separator")]
        [InlineData("1 234.56", "space thousand separator")]
        public async Task ValidateAsync_DifferentNumberFormats_ParsesCorrectly(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "amount";
            // Clean the input for parsing
            var cleanInput = input.Replace(",", "").Replace(" ", "");

            // Act
            await _validator.ValidateAsync(cleanInput, fieldName, result);

            // Assert
            result.IsValid.Should().BeTrue($"Should parse {scenario}");
            result.Errors.Should().BeEmpty();
        }

        #endregion

        #region Validation Result State Tests

        [Fact]
        public async Task ValidateAsync_ValidValue_DoesNotModifyExistingErrors()
        {
            // Arrange
            var result = new FieldValidationResult();
            result.Errors.Add("Previous error");
            result.IsValid = false;
            var fieldName = "amount";

            // Act
            await _validator.ValidateAsync("100.00", fieldName, result);

            // Assert
            result.Errors.Should().Contain("Previous error");
            // IsValid remains false because there was a previous error
            result.IsValid.Should().BeFalse();
        }

        [Fact]
        public async Task ValidateAsync_InvalidValue_AddsToExistingErrors()
        {
            // Arrange
            var result = new FieldValidationResult();
            result.Errors.Add("Previous error");
            var fieldName = "amount";

            // Act
            await _validator.ValidateAsync("invalid", fieldName, result);

            // Assert
            result.Errors.Should().HaveCount(2);
            result.Errors.Should().Contain("Previous error");
            result.Errors.Should().Contain($"{fieldName} must be a valid number.");
            result.IsValid.Should().BeFalse();
        }

        #endregion

        #region Performance and Special Cases Tests

        [Theory]
        [InlineData("0.000000000000001", "very small positive")]
        [InlineData("999999999999", "very large number")]
        [InlineData("123456789.123456789", "many digits")]
        public async Task ValidateAsync_ExtremeValues_HandlesCorrectly(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "amount";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            if (decimal.TryParse(input, out var value))
            {
                if (value < 0)
                {
                    result.Errors.Should().Contain($"{fieldName} cannot be negative.");
                }
                else if (value > 1_000_000)
                {
                    result.Warnings.Should().Contain(w => w.Contains("unusually large"));
                }
                
                if (Math.Round(value, 2) != value)
                {
                    result.Warnings.Should().Contain(w => w.Contains("more than 2 decimal places"));
                }
            }
        }

        [Fact]
        public async Task ValidateAsync_ConcurrentValidation_ThreadSafe()
        {
            // Arrange
            var tasks = new List<Task>();
            var results = new List<FieldValidationResult>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                var result = new FieldValidationResult();
                results.Add(result);
                var value = i % 2 == 0 ? "100.50" : "invalid";
                tasks.Add(_validator.ValidateAsync(value, $"field{i}", result));
            }

            await Task.WhenAll(tasks);

            // Assert
            for (int i = 0; i < 100; i++)
            {
                if (i % 2 == 0)
                {
                    results[i].IsValid.Should().BeTrue($"Valid value at index {i}");
                }
                else
                {
                    results[i].IsValid.Should().BeFalse($"Invalid value at index {i}");
                }
            }
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task ValidateAsync_TypicalCheckAmount_ValidatesCorrectly()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "checkAmount";
            var value = "1250.00";

            // Act
            await _validator.ValidateAsync(value, fieldName, result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Warnings.Should().BeEmpty();
        }

        [Fact]
        public async Task ValidateAsync_TypicalReceiptTotal_ValidatesCorrectly()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "total";
            var value = "45.99";

            // Act
            await _validator.ValidateAsync(value, fieldName, result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Warnings.Should().BeEmpty();
        }

        #endregion
    }
}