using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Services.Validation;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Services.Validation
{
    /// <summary>
    /// Comprehensive unit tests for W4SpecificRules with extensive coverage.
    /// Uses data-driven testing to validate W4 form validation logic across different scenarios.
    /// </summary>
    public class W4SpecificRulesTests
    {
        private readonly W4SpecificRules _rules;

        public W4SpecificRulesTests()
        {
            _rules = new W4SpecificRules();
        }

        #region DocumentType Property Tests

        [Fact]
        public void DocumentType_ReturnsW4()
        {
            // Act
            var documentType = _rules.DocumentType;

            // Assert
            documentType.Should().Be("w4");
        }

        #endregion

        #region CanHandle Method Tests

        [Theory]
        [InlineData("w4", true, "exact match")]
        [InlineData("W4", true, "uppercase match")]
        [InlineData("W4", true, "mixed case match")]
        [InlineData("check", false, "different document type")]
        [InlineData("receipt", false, "another document type")]
        [InlineData("", false, "empty string")]
        [InlineData(null, false, "null value")]
        public void CanHandle_VariousDocumentTypes_ReturnsExpected(string documentType, bool expected, string scenario)
        {
            // Arrange
            var fieldName = "ssn";

            // Act
            var result = _rules.CanHandle(fieldName, documentType);

            // Assert
            result.Should().Be(expected, $"Failed for {scenario}");
        }

        #endregion

        #region ApplyRulesAsync Tests - Null Value Handling

        [Fact]
        public async Task ApplyRulesAsync_NullValue_ReturnsWithoutProcessing()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("ssn", null, result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Warnings.Should().BeEmpty();
        }

        #endregion

        #region SSN Validation Tests

        [Theory]
        [InlineData("123-45-6789", true, 0, 1, "valid SSN with hyphens but test value")]
        [InlineData("123456789", true, 0, 1, "valid SSN without hyphens but test value")]
        [InlineData("987-65-4321", true, 0, 1, "test area number 987")]
        [InlineData("", false, 1, 0, "empty SSN")]
        [InlineData("   ", false, 1, 0, "whitespace SSN")]
        [InlineData(null, true, 0, 0, "null SSN")]
        [InlineData("000-12-3456", false, 1, 0, "invalid area number 000")]
        [InlineData("666-12-3456", false, 1, 0, "invalid area number 666")]
        [InlineData("900-12-3456", true, 0, 1, "test area number 900+")]
        [InlineData("123-00-4567", false, 1, 0, "invalid group number 00")]
        [InlineData("123-45-0000", false, 1, 0, "invalid serial number 0000")]
        [InlineData("111-11-1111", true, 0, 1, "test SSN all ones")]
        [InlineData("999-99-9999", true, 0, 2, "test SSN all nines with area warning")]
        [InlineData("12-345-6789", false, 1, 0, "invalid format")]
        [InlineData("abc-de-fghi", false, 1, 0, "non-numeric SSN")]
        [InlineData("123-45-678", false, 1, 0, "too short")]
        [InlineData("123-45-67890", false, 1, 0, "too long")]
        public async Task ValidateSSN_VariousFormats_ValidatesCorrectly(
            string ssn, bool shouldBeValid, int expectedErrors, int expectedWarnings, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("ssn", ssn, result);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, $"Failed for {scenario}");
            result.Errors.Count.Should().Be(expectedErrors, $"Error count mismatch for {scenario}");
            result.Warnings.Count.Should().Be(expectedWarnings, $"Warning count mismatch for {scenario}");

            if (shouldBeValid && !string.IsNullOrWhiteSpace(ssn))
            {
                result.Metadata.Should().ContainKey("normalizedSSN");
                result.Metadata["normalizedSSN"].Should().Be(ssn.Replace("-", ""));
            }
        }

        [Theory]
        [InlineData("socialsecuritynumber", "123-45-6789", "lowercase alias")]
        [InlineData("social_security_number", "123-45-6789", "underscore alias")]
        [InlineData("SSN", "123-45-6789", "uppercase field name")]
        [InlineData("SocialSecurityNumber", "123-45-6789", "camelCase alias")]
        public async Task ValidateSSN_DifferentFieldNames_RecognizesAllVariants(
            string fieldName, string ssn, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync(fieldName, ssn, result);

            // Assert
            result.IsValid.Should().BeTrue($"Failed for {scenario}");
            result.Metadata.Should().ContainKey("normalizedSSN", $"Should normalize SSN for {scenario}");
        }

        #endregion

        #region Withholding Validation Tests

        [Theory]
        [InlineData("0", true, 0, 0, "zero withholding")]
        [InlineData("100", true, 0, 0, "normal withholding")]
        [InlineData("100.50", true, 0, 0, "decimal withholding")]
        [InlineData("9999.99", true, 0, 0, "high but reasonable withholding")]
        [InlineData("10000", true, 0, 0, "exactly at threshold")]
        [InlineData("10001", true, 0, 1, "over threshold warning")]
        [InlineData("50000", true, 0, 1, "very high withholding")]
        [InlineData("-1", false, 1, 0, "negative withholding")]
        [InlineData("-100", false, 1, 0, "large negative withholding")]
        [InlineData("abc", false, 1, 0, "non-numeric withholding")]
        [InlineData("", false, 1, 0, "empty withholding")]
        [InlineData(null, true, 0, 0, "null withholding")]
        public async Task ValidateWithholding_VariousValues_ValidatesCorrectly(
            string withholding, bool shouldBeValid, int expectedErrors, int expectedWarnings, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("withholding", withholding, result);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, $"Failed for {scenario}");
            result.Errors.Count.Should().Be(expectedErrors, $"Error count mismatch for {scenario}");
            result.Warnings.Count.Should().Be(expectedWarnings, $"Warning count mismatch for {scenario}");

            if (shouldBeValid && decimal.TryParse(withholding, out var amount))
            {
                result.Metadata.Should().ContainKey("withholdingAmount");
                result.Metadata["withholdingAmount"].Should().Be(amount);
            }
        }

        [Theory]
        [InlineData("additionalwithholding", "500", "lowercase alias")]
        [InlineData("additional_withholding", "500", "underscore alias")]
        [InlineData("AdditionalWithholding", "500", "camelCase alias")]
        [InlineData("WITHHOLDING", "500", "uppercase field name")]
        public async Task ValidateWithholding_DifferentFieldNames_RecognizesAllVariants(
            string fieldName, string value, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync(fieldName, value, result);

            // Assert
            result.IsValid.Should().BeTrue($"Failed for {scenario}");
            result.Metadata.Should().ContainKey("withholdingAmount", $"Should store amount for {scenario}");
        }

        #endregion

        #region Allowances Validation Tests

        [Theory]
        [InlineData("0", true, 0, 0, "zero allowances")]
        [InlineData("1", true, 0, 0, "single allowance")]
        [InlineData("5", true, 0, 0, "typical allowances")]
        [InlineData("10", true, 0, 0, "high but reasonable")]
        [InlineData("20", true, 0, 0, "exactly at threshold")]
        [InlineData("21", true, 0, 1, "over threshold warning")]
        [InlineData("50", true, 0, 1, "very high allowances")]
        [InlineData("-1", false, 1, 0, "negative allowances")]
        [InlineData("3.5", false, 1, 0, "decimal allowances")]
        [InlineData("abc", false, 1, 0, "non-numeric allowances")]
        [InlineData("", false, 1, 0, "empty allowances")]
        public async Task ValidateAllowances_VariousValues_ValidatesCorrectly(
            string allowances, bool shouldBeValid, int expectedErrors, int expectedWarnings, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("allowances", allowances, result);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, $"Failed for {scenario}");
            result.Errors.Count.Should().Be(expectedErrors, $"Error count mismatch for {scenario}");
            result.Warnings.Count.Should().Be(expectedWarnings, $"Warning count mismatch for {scenario}");

            if (shouldBeValid && int.TryParse(allowances, out var count))
            {
                result.Metadata.Should().ContainKey("allowancesCount");
                result.Metadata["allowancesCount"].Should().Be(count);
            }
        }

        #endregion

        #region Filing Status Validation Tests

        [Theory]
        [InlineData("Single", true, 0, "valid single status")]
        [InlineData("single", true, 0, "lowercase single")]
        [InlineData("SINGLE", true, 0, "uppercase single")]
        [InlineData("Married", true, 0, "valid married status")]
        [InlineData("Married Filing Separately", true, 0, "valid married filing separately")]
        [InlineData("marriedfilingseparately", true, 0, "no spaces variant")]
        [InlineData("Head of Household", true, 0, "valid head of household")]
        [InlineData("headofhousehold", true, 0, "no spaces lowercase")]
        [InlineData("", false, 1, "empty status")]
        [InlineData("   ", false, 1, "whitespace status")]
        [InlineData(null, true, 0, "null status")]
        [InlineData("Divorced", false, 1, "invalid status")]
        [InlineData("Widowed", false, 1, "another invalid status")]
        [InlineData("Unknown", false, 1, "unknown status")]
        public async Task ValidateFilingStatus_VariousStatuses_ValidatesCorrectly(
            string status, bool shouldBeValid, int expectedErrors, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("filingstatus", status, result);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, $"Failed for {scenario}");
            result.Errors.Count.Should().Be(expectedErrors, $"Error count mismatch for {scenario}");

            if (shouldBeValid && !string.IsNullOrWhiteSpace(status))
            {
                result.Metadata.Should().ContainKey("filingStatus");
                result.Metadata["filingStatus"].Should().Be(status);
            }
        }

        [Theory]
        [InlineData("filing_status", "Single", "underscore alias")]
        [InlineData("maritalstatus", "Married", "marital status alias")]
        [InlineData("marital_status", "Single", "marital status with underscore")]
        [InlineData("FilingStatus", "Single", "camelCase")]
        public async Task ValidateFilingStatus_DifferentFieldNames_RecognizesAllVariants(
            string fieldName, string value, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync(fieldName, value, result);

            // Assert
            result.IsValid.Should().BeTrue($"Failed for {scenario}");
            result.Metadata.Should().ContainKey("filingStatus", $"Should store status for {scenario}");
        }

        #endregion

        #region Dependents Validation Tests

        [Theory]
        [InlineData("0", true, 0, 0, "no dependents")]
        [InlineData("1", true, 0, 0, "single dependent")]
        [InlineData("3", true, 0, 0, "typical family")]
        [InlineData("10", true, 0, 0, "large family")]
        [InlineData("20", true, 0, 0, "exactly at threshold")]
        [InlineData("21", true, 0, 1, "over threshold warning")]
        [InlineData("30", true, 0, 1, "very high dependents")]
        [InlineData("-1", false, 1, 0, "negative dependents")]
        [InlineData("2.5", false, 1, 0, "fractional dependents")]
        [InlineData("abc", false, 1, 0, "non-numeric dependents")]
        public async Task ValidateDependents_VariousValues_ValidatesCorrectly(
            string dependents, bool shouldBeValid, int expectedErrors, int expectedWarnings, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("dependents", dependents, result);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, $"Failed for {scenario}");
            result.Errors.Count.Should().Be(expectedErrors, $"Error count mismatch for {scenario}");
            result.Warnings.Count.Should().Be(expectedWarnings, $"Warning count mismatch for {scenario}");
        }

        #endregion

        #region Multiple Jobs Worksheet Validation Tests

        [Theory]
        [InlineData("0", true, 0, 0, "zero amount")]
        [InlineData("1000", true, 0, 0, "normal amount")]
        [InlineData("50000", true, 0, 0, "exactly at threshold")]
        [InlineData("50001", true, 0, 1, "over threshold")]
        [InlineData("-100", false, 1, 0, "negative amount")]
        [InlineData("abc", true, 0, 0, "non-numeric ignored as optional")]
        [InlineData("", true, 0, 0, "empty optional field")]
        [InlineData(null, true, 0, 0, "null optional field")]
        public async Task ValidateMultipleJobsWorksheet_VariousValues_ValidatesCorrectly(
            string amount, bool shouldBeValid, int expectedErrors, int expectedWarnings, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("multipleworksheet", amount, result);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, $"Failed for {scenario}");
            result.Errors.Count.Should().Be(expectedErrors, $"Error count mismatch for {scenario}");
            result.Warnings.Count.Should().Be(expectedWarnings, $"Warning count mismatch for {scenario}");
        }

        #endregion

        #region Other Income Validation Tests

        [Theory]
        [InlineData("0", true, 0, 0, "zero income")]
        [InlineData("5000", true, 0, 0, "normal income")]
        [InlineData("100000", true, 0, 0, "exactly at threshold")]
        [InlineData("100001", true, 0, 1, "over threshold")]
        [InlineData("-500", true, 0, 1, "negative income warning")]
        [InlineData("abc", true, 0, 0, "non-numeric ignored as optional")]
        [InlineData("", true, 0, 0, "empty optional field")]
        public async Task ValidateOtherIncome_VariousValues_ValidatesCorrectly(
            string income, bool shouldBeValid, int expectedErrors, int expectedWarnings, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("othersincome", income, result);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, $"Failed for {scenario}");
            result.Errors.Count.Should().Be(expectedErrors, $"Error count mismatch for {scenario}");
            result.Warnings.Count.Should().Be(expectedWarnings, $"Warning count mismatch for {scenario}");
        }

        #endregion

        #region Deductions Validation Tests

        [Theory]
        [InlineData("0", true, 0, 0, "zero deductions")]
        [InlineData("12000", true, 0, 0, "standard deduction")]
        [InlineData("100000", true, 0, 0, "exactly at threshold")]
        [InlineData("100001", true, 0, 1, "over threshold")]
        [InlineData("-100", false, 1, 0, "negative deductions")]
        [InlineData("abc", true, 0, 0, "non-numeric ignored as optional")]
        public async Task ValidateDeductions_VariousValues_ValidatesCorrectly(
            string deductions, bool shouldBeValid, int expectedErrors, int expectedWarnings, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("deductions", deductions, result);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, $"Failed for {scenario}");
            result.Errors.Count.Should().Be(expectedErrors, $"Error count mismatch for {scenario}");
            result.Warnings.Count.Should().Be(expectedWarnings, $"Warning count mismatch for {scenario}");
        }

        #endregion

        #region Extra Withholding Validation Tests

        [Theory]
        [InlineData("0", true, 0, 0, "zero extra")]
        [InlineData("100", true, 0, 0, "normal extra")]
        [InlineData("5000", true, 0, 0, "exactly at threshold")]
        [InlineData("5001", true, 0, 1, "over threshold")]
        [InlineData("-50", false, 1, 0, "negative extra")]
        [InlineData("abc", true, 0, 0, "non-numeric ignored as optional")]
        public async Task ValidateExtraWithholding_VariousValues_ValidatesCorrectly(
            string extra, bool shouldBeValid, int expectedErrors, int expectedWarnings, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("extrawithholding", extra, result);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, $"Failed for {scenario}");
            result.Errors.Count.Should().Be(expectedErrors, $"Error count mismatch for {scenario}");
            result.Warnings.Count.Should().Be(expectedWarnings, $"Warning count mismatch for {scenario}");
        }

        #endregion

        #region Exempt Status Validation Tests

        [Theory]
        [InlineData("true", true, true, 1, "boolean true string")]
        [InlineData("True", true, true, 1, "boolean True capitalized")]
        [InlineData("yes", true, true, 1, "yes value")]
        [InlineData("YES", true, true, 1, "YES uppercase")]
        [InlineData("1", true, true, 1, "numeric 1")]
        [InlineData("exempt", true, true, 1, "exempt string")]
        [InlineData("EXEMPT", true, true, 1, "EXEMPT uppercase")]
        [InlineData("false", true, false, 0, "boolean false string")]
        [InlineData("no", true, false, 0, "no value")]
        [InlineData("0", true, false, 0, "numeric 0")]
        [InlineData("", true, false, 0, "empty string")]
        [InlineData(null, true, false, 0, "null value")]
        [InlineData("unknown", true, false, 0, "unknown value")]
        public async Task ValidateExemptStatus_VariousValues_ValidatesCorrectly(
            string exempt, bool shouldBeValid, bool expectedExempt, int expectedWarnings, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("exempt", exempt, result);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, $"Failed for {scenario}");
            result.Warnings.Count.Should().Be(expectedWarnings, $"Warning count mismatch for {scenario}");
            result.Metadata.Should().ContainKey("isExempt");
            result.Metadata["isExempt"].Should().Be(expectedExempt, $"Exempt status mismatch for {scenario}");
        }

        [Fact]
        public async Task ValidateExemptStatus_BooleanTrue_RecognizesExempt()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("exempt", true, result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Warnings.Should().ContainSingle();
            result.Metadata["isExempt"].Should().Be(true);
        }

        [Fact]
        public async Task ValidateExemptStatus_BooleanFalse_RecognizesNotExempt()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("exempt", false, result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Warnings.Should().BeEmpty();
            result.Metadata["isExempt"].Should().Be(false);
        }

        #endregion

        #region Tax Year Validation Tests

        [Theory]
        [InlineData("2024", true, 0, 0, "current year")]
        [InlineData("2023", true, 0, 0, "last year")]
        [InlineData("2025", true, 0, 0, "next year allowed")]
        [InlineData("2020", true, 0, 0, "within 5 years")]
        [InlineData("2019", true, 0, 1, "exactly 5 years old")]
        [InlineData("2018", true, 0, 1, "more than 5 years old")]
        [InlineData("2027", false, 1, 0, "too far in future")]
        [InlineData("2030", false, 1, 0, "way too far in future")]
        [InlineData("abc", true, 0, 0, "non-numeric ignored as optional")]
        [InlineData("", true, 0, 0, "empty optional field")]
        public async Task ValidateTaxYear_VariousYears_ValidatesCorrectly(
            string year, bool shouldBeValid, int expectedErrors, int expectedWarnings, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();
            var currentYear = DateTime.Now.Year;

            // Adjust expectations based on current year
            if (int.TryParse(year, out var yearValue))
            {
                if (yearValue < currentYear - 5)
                {
                    expectedWarnings = 1;
                }
                else if (yearValue > currentYear + 1)
                {
                    shouldBeValid = false;
                    expectedErrors = 1;
                }
                else
                {
                    expectedWarnings = 0;
                    expectedErrors = 0;
                }
            }

            // Act
            await _rules.ApplyRulesAsync("taxyear", year, result);

            // Assert
            result.IsValid.Should().Be(shouldBeValid, $"Failed for {scenario}");
            result.Errors.Count.Should().Be(expectedErrors, $"Error count mismatch for {scenario}");
            result.Warnings.Count.Should().Be(expectedWarnings, $"Warning count mismatch for {scenario}");
        }

        #endregion

        #region Field Name Variations Tests

        [Theory]
        [InlineData("tax_year", "2024", "underscore variant")]
        [InlineData("TaxYear", "2024", "camelCase variant")]
        [InlineData("YEAR", "2024", "uppercase variant")]
        [InlineData("other_income", "5000", "underscore income")]
        [InlineData("OthersIncome", "5000", "camelCase income")]
        [InlineData("itemized_deductions", "12000", "underscore deductions")]
        [InlineData("ItemizedDeductions", "12000", "camelCase deductions")]
        [InlineData("extra_withholding", "100", "underscore extra")]
        [InlineData("ExtraWithholding", "100", "camelCase extra")]
        public async Task ApplyRulesAsync_FieldNameVariations_RecognizesAllVariants(
            string fieldName, string value, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync(fieldName, value, result);

            // Assert
            result.IsValid.Should().BeTrue($"Failed for {scenario}");
            result.Metadata.Should().NotBeEmpty($"Should have metadata for {scenario}");
        }

        #endregion

        #region Unknown Field Handling Tests

        [Theory]
        [InlineData("unknownfield", "value", "completely unknown field")]
        [InlineData("randomfield", "123", "random field with numeric value")]
        [InlineData("notaw4field", "test", "clearly not a W4 field")]
        public async Task ApplyRulesAsync_UnknownFields_DoesNotProcess(
            string fieldName, string value, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync(fieldName, value, result);

            // Assert
            result.IsValid.Should().BeTrue($"Should not invalidate for {scenario}");
            result.Errors.Should().BeEmpty($"Should not add errors for {scenario}");
            result.Warnings.Should().BeEmpty($"Should not add warnings for {scenario}");
            result.Metadata.Should().BeEmpty($"Should not add metadata for {scenario}");
        }

        #endregion

        #region Cross-Field Validation Scenarios

        [Fact]
        public async Task MultipleFields_ValidW4Form_AllFieldsValidate()
        {
            // Arrange
            var fields = new Dictionary<string, object>
            {
                { "ssn", "123-45-6789" },
                { "withholding", "500" },
                { "allowances", "2" },
                { "filingstatus", "Married" },
                { "dependents", "2" },
                { "taxyear", DateTime.Now.Year.ToString() }
            };

            // Act & Assert
            foreach (var field in fields)
            {
                var result = new FieldValidationResult();
                await _rules.ApplyRulesAsync(field.Key, field.Value, result);
                
                result.IsValid.Should().BeTrue($"Field {field.Key} should be valid");
                result.Errors.Should().BeEmpty($"Field {field.Key} should have no errors");
            }
        }

        [Fact]
        public async Task MultipleFields_InvalidW4Form_AllFieldsHaveErrors()
        {
            // Arrange
            var fields = new Dictionary<string, object>
            {
                { "ssn", "000-00-0000" },
                { "withholding", "-100" },
                { "allowances", "-5" },
                { "filingstatus", "InvalidStatus" },
                { "dependents", "-2" },
                { "taxyear", (DateTime.Now.Year + 5).ToString() }
            };

            // Act & Assert
            foreach (var field in fields)
            {
                var result = new FieldValidationResult();
                await _rules.ApplyRulesAsync(field.Key, field.Value, result);
                
                result.IsValid.Should().BeFalse($"Field {field.Key} should be invalid");
                result.Errors.Should().NotBeEmpty($"Field {field.Key} should have errors");
            }
        }

        #endregion

        #region Edge Cases and Boundary Tests

        [Theory]
        [InlineData("ssn", int.MaxValue, "integer max value for SSN")]
        [InlineData("withholding", double.MaxValue, "double max value for withholding")]
        [InlineData("allowances", long.MaxValue, "long max value for allowances")]
        [InlineData("dependents", byte.MaxValue, "byte max value for dependents")]
        public async Task ApplyRulesAsync_NumericTypeVariations_HandlesCorrectly(
            string fieldName, object value, string scenario)
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync(fieldName, value, result);

            // Assert
            // Should handle conversion or fail gracefully
            result.Should().NotBeNull($"Result should not be null for {scenario}");
        }

        [Fact]
        public async Task ApplyRulesAsync_VeryLongStrings_HandlesGracefully()
        {
            // Arrange
            var result = new FieldValidationResult();
            var longString = new string('1', 1000);

            // Act
            await _rules.ApplyRulesAsync("ssn", longString, result);

            // Assert
            result.IsValid.Should().BeFalse("Very long SSN should be invalid");
            result.Errors.Should().NotBeEmpty("Should have format error");
        }

        [Fact]
        public async Task ApplyRulesAsync_SpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var result = new FieldValidationResult();
            var specialChars = "!@#$%^&*()";

            // Act
            await _rules.ApplyRulesAsync("ssn", specialChars, result);

            // Assert
            result.IsValid.Should().BeFalse("Special characters in SSN should be invalid");
            result.Errors.Should().NotBeEmpty("Should have format error");
        }

        #endregion

        #region Metadata Storage Tests

        [Fact]
        public async Task ValidateSSN_ValidSSN_StoresNormalizedVersion()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("ssn", "123-45-6789", result);

            // Assert
            result.Metadata.Should().ContainKey("normalizedSSN");
            result.Metadata["normalizedSSN"].Should().Be("123456789");
        }

        [Fact]
        public async Task ValidateWithholding_ValidAmount_StoresDecimalValue()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("withholding", "1234.56", result);

            // Assert
            result.Metadata.Should().ContainKey("withholdingAmount");
            result.Metadata["withholdingAmount"].Should().Be(1234.56m);
        }

        [Fact]
        public async Task ValidateAllowances_ValidCount_StoresIntegerValue()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            await _rules.ApplyRulesAsync("allowances", "5", result);

            // Assert
            result.Metadata.Should().ContainKey("allowancesCount");
            result.Metadata["allowancesCount"].Should().Be(5);
        }

        #endregion

        #region Performance and Concurrency Tests

        [Fact]
        public async Task ApplyRulesAsync_ConcurrentCalls_ThreadSafe()
        {
            // Arrange
            var tasks = new List<Task>();
            var fieldNames = new[] { "ssn", "withholding", "allowances", "filingstatus", "dependents" };
            var values = new[] { "123-45-6789", "500", "2", "Single", "1" };

            // Act
            for (int i = 0; i < 100; i++)
            {
                var index = i % fieldNames.Length;
                tasks.Add(Task.Run(async () =>
                {
                    var result = new FieldValidationResult();
                    await _rules.ApplyRulesAsync(fieldNames[index], values[index], result);
                    result.IsValid.Should().BeTrue();
                }));
            }

            // Assert
            await Task.WhenAll(tasks);
            // If we get here without exceptions, the validator is thread-safe
        }

        #endregion

        #region Direct Method Tests for Public Validation Methods

        [Fact]
        public void ValidateSSN_DirectCall_ValidSSN_Succeeds()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            _rules.ValidateSSN("123-45-6789", result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Metadata["normalizedSSN"].Should().Be("123456789");
        }

        [Fact]
        public void ValidateWithholding_DirectCall_ValidAmount_Succeeds()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            _rules.ValidateWithholding("500.50", result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Metadata["withholdingAmount"].Should().Be(500.50m);
        }

        [Fact]
        public void ValidateAllowances_DirectCall_ValidCount_Succeeds()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            _rules.ValidateAllowances("3", result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Metadata["allowancesCount"].Should().Be(3);
        }

        [Fact]
        public void ValidateFilingStatus_DirectCall_ValidStatus_Succeeds()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            _rules.ValidateFilingStatus("Single", result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Metadata["filingStatus"].Should().Be("Single");
        }

        [Fact]
        public void ValidateDependents_DirectCall_ValidCount_Succeeds()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            _rules.ValidateDependents("2", result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Metadata["dependentsCount"].Should().Be(2);
        }

        [Fact]
        public void ValidateMultipleJobsWorksheet_DirectCall_ValidAmount_Succeeds()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            _rules.ValidateMultipleJobsWorksheet("10000", result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Metadata["multipleJobsAmount"].Should().Be(10000m);
        }

        [Fact]
        public void ValidateOtherIncome_DirectCall_ValidAmount_Succeeds()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            _rules.ValidateOtherIncome("5000", result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Warnings.Should().BeEmpty();
            result.Metadata["otherIncome"].Should().Be(5000m);
        }

        [Fact]
        public void ValidateDeductions_DirectCall_ValidAmount_Succeeds()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            _rules.ValidateDeductions("12000", result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Metadata["deductionsAmount"].Should().Be(12000m);
        }

        [Fact]
        public void ValidateExtraWithholding_DirectCall_ValidAmount_Succeeds()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            _rules.ValidateExtraWithholding("100", result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Metadata["extraWithholding"].Should().Be(100m);
        }

        [Fact]
        public void ValidateExemptStatus_DirectCall_ExemptTrue_AddsWarning()
        {
            // Arrange
            var result = new FieldValidationResult();

            // Act
            _rules.ValidateExemptStatus("true", result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Warnings.Should().ContainSingle();
            result.Metadata["isExempt"].Should().Be(true);
        }

        [Fact]
        public void ValidateTaxYear_DirectCall_ValidYear_Succeeds()
        {
            // Arrange
            var result = new FieldValidationResult();
            var currentYear = DateTime.Now.Year;

            // Act
            _rules.ValidateTaxYear(currentYear.ToString(), result);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Metadata["taxYear"].Should().Be(currentYear);
        }

        #endregion
    }
}