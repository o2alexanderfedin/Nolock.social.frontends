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
    /// Comprehensive unit tests for IntegerFieldValidator with extensive coverage.
    /// Uses data-driven testing to validate integer validation logic across different scenarios.
    /// </summary>
    public class IntegerFieldValidatorTests
    {
        private readonly IntegerFieldValidator _validator;

        public IntegerFieldValidatorTests()
        {
            _validator = new IntegerFieldValidator();
        }

        #region FieldType Property Tests

        [Fact]
        public void FieldType_ReturnsInteger()
        {
            // Act
            var fieldType = _validator.FieldType;

            // Assert
            fieldType.Should().Be(FieldType.Integer);
        }

        #endregion

        #region Valid Integer Formats Tests

        [Theory]
        [InlineData("0", "zero")]
        [InlineData("1", "single positive digit")]
        [InlineData("-1", "single negative digit")]
        [InlineData("123", "positive integer")]
        [InlineData("-123", "negative integer")]
        [InlineData("999999999", "large positive integer")]
        [InlineData("-999999999", "large negative integer")]
        [InlineData("2147483647", "int.MaxValue")]
        [InlineData("-2147483648", "int.MinValue")]
        [InlineData("+123", "positive sign prefix")]
        [InlineData("00123", "leading zeros")]
        [InlineData("-00123", "negative with leading zeros")]
        public async Task ValidateAsync_ValidIntegerFormats_ReturnsValid(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            result.IsValid.Should().BeTrue($"Failed for {scenario}: {input}");
            result.Errors.Should().BeEmpty($"Should have no errors for {scenario}");
        }

        [Theory]
        [InlineData(0, "zero as int")]
        [InlineData(123, "positive int")]
        [InlineData(-123, "negative int")]
        [InlineData(int.MaxValue, "int.MaxValue as int")]
        [InlineData(int.MinValue, "int.MinValue as int")]
        public async Task ValidateAsync_ValidIntegerObjects_ReturnsValid(int input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            result.IsValid.Should().BeTrue($"Failed for {scenario}: {input}");
            result.Errors.Should().BeEmpty($"Should have no errors for {scenario}");
        }

        #endregion

        #region Invalid Integer Formats Tests

        [Theory]
        [InlineData("123.45", "decimal number")]
        [InlineData("123.0", "decimal with zero fraction")]
        [InlineData("1.23e5", "scientific notation lowercase")]
        [InlineData("1.23E5", "scientific notation uppercase")]
        [InlineData("1e10", "scientific notation integer")]
        [InlineData("abc", "alphabetic text")]
        [InlineData("123abc", "mixed alphanumeric")]
        [InlineData("abc123", "alphanumeric starting with letters")]
        [InlineData("12 34", "number with space")]
        [InlineData("12-34", "number with dash in middle")]
        [InlineData("$123", "currency symbol")]
        [InlineData("123%", "percentage symbol")]
        [InlineData("#123", "hash symbol")]
        [InlineData("@123", "at symbol")]
        [InlineData("123!", "exclamation mark")]
        [InlineData("(123)", "parentheses")]
        [InlineData("[123]", "square brackets")]
        [InlineData("{123}", "curly brackets")]
        [InlineData("12.34.56", "multiple decimal points")]
        [InlineData("++123", "double positive sign")]
        [InlineData("--123", "double negative sign")]
        [InlineData("+-123", "mixed signs")]
        [InlineData("-+123", "mixed signs reversed")]
        [InlineData("∞", "infinity symbol")]
        [InlineData("NaN", "not a number")]
        [InlineData("null", "null string")]
        [InlineData("undefined", "undefined string")]
        [InlineData("true", "boolean true")]
        [InlineData("false", "boolean false")]
        [InlineData("0x123", "hexadecimal")]
        [InlineData("0b1010", "binary")]
        [InlineData("0o123", "octal")]
        public async Task ValidateAsync_InvalidFormats_ReturnsInvalid(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse($"Should be invalid for {scenario}: {input}");
            result.Errors.Should().ContainSingle()
                .Which.Should().Be($"{fieldName} must be a valid whole number.");
        }

        #endregion

        #region Edge Cases and Boundary Tests

        [Theory]
        [InlineData("", "empty string")]
        [InlineData(" ", "single space")]
        [InlineData("  ", "multiple spaces")]
        [InlineData("\t", "tab character")]
        [InlineData("\n", "newline character")]
        [InlineData("\r\n", "carriage return and newline")]
        public async Task ValidateAsync_EmptyOrWhitespace_ReturnsInvalid(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse($"Should be invalid for {scenario}");
            result.Errors.Should().ContainSingle()
                .Which.Should().Be($"{fieldName} must be a valid whole number.");
        }

        [Fact]
        public async Task ValidateAsync_NullInput_ThrowsNullReferenceException()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";
            object? nullInput = null;

            // Act & Assert
            // Note: The current implementation throws NullReferenceException for null input
            // This test documents the actual behavior
            var act = async () => await _validator.ValidateAsync(nullInput!, fieldName, result);
            await act.Should().ThrowAsync<NullReferenceException>();
        }

        [Theory]
        [InlineData("2147483648", "int.MaxValue + 1")]
        [InlineData("-2147483649", "int.MinValue - 1")]
        [InlineData("9999999999999999999", "very large number")]
        [InlineData("-9999999999999999999", "very large negative number")]
        public async Task ValidateAsync_OverflowValues_ReturnsInvalid(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse($"Should be invalid for overflow {scenario}: {input}");
            result.Errors.Should().ContainSingle()
                .Which.Should().Be($"{fieldName} must be a valid whole number.");
        }

        #endregion

        #region Whitespace Handling Tests

        [Theory]
        [InlineData(" 123", "leading space")]
        [InlineData("123 ", "trailing space")]
        [InlineData(" 123 ", "leading and trailing spaces")]
        [InlineData("  123  ", "multiple leading and trailing spaces")]
        [InlineData("\t123", "leading tab")]
        [InlineData("123\t", "trailing tab")]
        [InlineData("\t123\t", "leading and trailing tabs")]
        [InlineData(" \t123\t ", "mixed whitespace")]
        public async Task ValidateAsync_WhitespaceAroundValidInteger_ReturnsValid(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            // Note: int.TryParse with default settings DOES trim whitespace, so these are valid
            result.IsValid.Should().BeTrue($"Should be valid for {scenario}: '{input}'");
            result.Errors.Should().BeEmpty($"Should have no errors for {scenario}");
        }

        [Theory]
        [InlineData("\r\n123", "leading newline")]
        [InlineData("123\r\n", "trailing newline")]
        [InlineData("\r\n123\r\n", "leading and trailing newlines")]
        public async Task ValidateAsync_NewlineAroundValidInteger_ReturnsValid(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            // Note: int.TryParse actually handles newlines and treats them as whitespace
            result.IsValid.Should().BeTrue($"Should be valid for {scenario}: '{input}'");
            result.Errors.Should().BeEmpty($"Should have no errors for {scenario}");
        }

        #endregion

        #region Number Formatting Tests

        [Theory]
        [InlineData("1,234", "thousands separator")]
        [InlineData("1,234,567", "multiple thousands separators")]
        [InlineData("123,456,789", "large number with separators")]
        [InlineData("1.234", "European thousands separator")]
        [InlineData("1'234", "Swiss thousands separator")]
        [InlineData("1_234", "underscore separator")]
        [InlineData("1 234", "space separator")]
        public async Task ValidateAsync_FormattedNumbers_ReturnsInvalid(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse($"Should be invalid for {scenario}: {input}");
            result.Errors.Should().ContainSingle()
                .Which.Should().Be($"{fieldName} must be a valid whole number.");
        }

        #endregion

        #region Special Characters Tests

        [Theory]
        [InlineData("½", "fraction symbol")]
        [InlineData("¼", "quarter symbol")]
        [InlineData("¾", "three quarters symbol")]
        [InlineData("²", "superscript 2")]
        [InlineData("³", "superscript 3")]
        [InlineData("₁", "subscript 1")]
        [InlineData("₂", "subscript 2")]
        [InlineData("①", "circled 1")]
        [InlineData("②", "circled 2")]
        [InlineData("Ⅰ", "Roman numeral I")]
        [InlineData("Ⅱ", "Roman numeral II")]
        [InlineData("Ⅲ", "Roman numeral III")]
        [InlineData("π", "pi symbol")]
        [InlineData("√", "square root symbol")]
        [InlineData("±", "plus-minus symbol")]
        [InlineData("≈", "approximately symbol")]
        [InlineData("≠", "not equal symbol")]
        [InlineData("≤", "less than or equal")]
        [InlineData("≥", "greater than or equal")]
        public async Task ValidateAsync_SpecialMathCharacters_ReturnsInvalid(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse($"Should be invalid for {scenario}: {input}");
            result.Errors.Should().ContainSingle()
                .Which.Should().Be($"{fieldName} must be a valid whole number.");
        }

        #endregion

        #region Different Types Input Tests

        [Fact]
        public async Task ValidateAsync_DoubleInput_ReturnsInvalid()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";
            double doubleInput = 123.45;

            // Act
            await _validator.ValidateAsync(doubleInput, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse("Should be invalid for double input");
            result.Errors.Should().ContainSingle()
                .Which.Should().Be($"{fieldName} must be a valid whole number.");
        }

        [Fact]
        public async Task ValidateAsync_DecimalInput_ReturnsInvalid()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";
            decimal decimalInput = 123.45m;

            // Act
            await _validator.ValidateAsync(decimalInput, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse("Should be invalid for decimal input");
            result.Errors.Should().ContainSingle()
                .Which.Should().Be($"{fieldName} must be a valid whole number.");
        }

        [Fact]
        public async Task ValidateAsync_FloatInput_ReturnsInvalid()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";
            float floatInput = 123.45f;

            // Act
            await _validator.ValidateAsync(floatInput, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse("Should be invalid for float input");
            result.Errors.Should().ContainSingle()
                .Which.Should().Be($"{fieldName} must be a valid whole number.");
        }

        [Fact]
        public async Task ValidateAsync_BooleanTrueInput_ReturnsInvalid()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";
            bool boolInput = true;

            // Act
            await _validator.ValidateAsync(boolInput, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse("Should be invalid for boolean input");
            result.Errors.Should().ContainSingle()
                .Which.Should().Be($"{fieldName} must be a valid whole number.");
        }

        [Fact]
        public async Task ValidateAsync_DateTimeInput_ReturnsInvalid()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";
            DateTime dateInput = DateTime.Now;

            // Act
            await _validator.ValidateAsync(dateInput, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse("Should be invalid for DateTime input");
            result.Errors.Should().ContainSingle()
                .Which.Should().Be($"{fieldName} must be a valid whole number.");
        }

        [Fact]
        public async Task ValidateAsync_GuidInput_ReturnsInvalid()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";
            Guid guidInput = Guid.NewGuid();

            // Act
            await _validator.ValidateAsync(guidInput, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse("Should be invalid for Guid input");
            result.Errors.Should().ContainSingle()
                .Which.Should().Be($"{fieldName} must be a valid whole number.");
        }

        #endregion

        #region Validation Message Tests

        [Theory]
        [InlineData("invalidInput", "testField", "testField must be a valid whole number.")]
        [InlineData("123.45", "amountField", "amountField must be a valid whole number.")]
        [InlineData("abc", "quantityField", "quantityField must be a valid whole number.")]
        [InlineData("", "countField", "countField must be a valid whole number.")]
        public async Task ValidateAsync_InvalidInput_GeneratesCorrectErrorMessage(string input, string fieldName, string expectedMessage)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle()
                .Which.Should().Be(expectedMessage);
        }

        #endregion

        #region Multiple Validation Calls Tests

        [Fact]
        public async Task ValidateAsync_MultipleValidations_AccumulatesErrors()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act - First validation
            await _validator.ValidateAsync("invalid1", "field1", result);
            
            // Assert - After first validation
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle()
                .Which.Should().Be("field1 must be a valid whole number.");

            // Act - Second validation (should accumulate errors)
            await _validator.ValidateAsync("invalid2", "field2", result);

            // Assert - After second validation
            result.IsValid.Should().BeFalse();
            result.Errors.Should().HaveCount(2);
            result.Errors.Should().Contain("field1 must be a valid whole number.");
            result.Errors.Should().Contain("field2 must be a valid whole number.");
        }

        [Fact]
        public async Task ValidateAsync_ValidAfterInvalid_RemainsInvalid()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act - First invalid validation
            await _validator.ValidateAsync("invalid", "field1", result);
            
            // Assert - After invalid
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle();

            // Act - Second valid validation
            await _validator.ValidateAsync("123", "field2", result);

            // Assert - Still invalid due to first error
            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle()
                .Which.Should().Be("field1 must be a valid whole number.");
        }

        #endregion

        #region Culture-Specific Tests

        [Theory]
        [InlineData("١٢٣", "Arabic numerals")]
        [InlineData("१२३", "Hindi numerals")]
        [InlineData("一二三", "Chinese numerals")]
        [InlineData("๑๒๓", "Thai numerals")]
        [InlineData("௧௨௩", "Tamil numerals")]
        public async Task ValidateAsync_NonLatinNumerals_ReturnsInvalid(string input, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "integerField";

            // Act
            await _validator.ValidateAsync(input, fieldName, result);

            // Assert
            result.IsValid.Should().BeFalse($"Should be invalid for {scenario}: {input}");
            result.Errors.Should().ContainSingle()
                .Which.Should().Be($"{fieldName} must be a valid whole number.");
        }

        #endregion

        #region Task Completion Tests

        [Fact]
        public async Task ValidateAsync_AlwaysCompletesTask()
        {
            // Arrange
            var result = new FieldValidationResult();
            var fieldName = "testField";

            // Act
            var task = _validator.ValidateAsync("123", fieldName, result);

            // Assert
            task.IsCompleted.Should().BeTrue("Task should complete synchronously");
            await task; // Should not throw
        }

        #endregion

        #region Concurrent Validation Tests

        [Fact]
        public async Task ValidateAsync_ConcurrentValidations_WorksCorrectly()
        {
            // Arrange
            var tasks = new List<Task>();
            var results = new List<FieldValidationResult>();

            // Act - Create multiple validation tasks
            for (int i = 0; i < 10; i++)
            {
                var result = new FieldValidationResult();
                results.Add(result);
                var isValid = i % 2 == 0;
                var input = isValid ? i.ToString() : $"invalid{i}";
                tasks.Add(_validator.ValidateAsync(input, $"field{i}", result));
            }

            await Task.WhenAll(tasks);

            // Assert
            for (int i = 0; i < 10; i++)
            {
                var isValid = i % 2 == 0;
                results[i].IsValid.Should().Be(isValid, $"Result {i} validation should be {isValid}");
                
                if (!isValid)
                {
                    results[i].Errors.Should().ContainSingle()
                        .Which.Should().Be($"field{i} must be a valid whole number.");
                }
                else
                {
                    results[i].Errors.Should().BeEmpty();
                }
            }
        }

        #endregion
    }
}