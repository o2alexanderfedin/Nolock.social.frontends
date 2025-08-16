using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Processors;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Processors
{
    /// <summary>
    /// Unit tests for the W4Processor class.
    /// </summary>
    public class W4ProcessorTests
    {
        private readonly Mock<ILogger<W4Processor>> _mockLogger;
        private readonly W4Processor _processor;

        public W4ProcessorTests()
        {
            _mockLogger = new Mock<ILogger<W4Processor>>();
            _processor = new W4Processor(_mockLogger.Object);
        }

        #region CanProcess Tests

        [Theory]
        [InlineData("Form W-4 Employee's Withholding Certificate", true, "Should recognize W-4 form title")]
        [InlineData("Form W4 2020 withholding allowance single married", true, "Should recognize W-4 with keywords")]
        [InlineData("W-4 Social Security Number Filing Status Dependents", true, "Should recognize W-4 with multiple keywords")]
        [InlineData("Random receipt with total amount tax", false, "Should not recognize non-W4 document")]
        [InlineData("", false, "Should reject empty input")]
        [InlineData(null, false, "Should reject null input")]
        public void CanProcess_WithVariousInputs_ReturnsExpectedResult(string input, bool expected, string scenario)
        {
            // Act
            var result = _processor.CanProcess(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region ProcessAsync Tests

        [Fact]
        public async Task ProcessAsync_WithValidPost2020W4_ExtractsAllFields()
        {
            // Arrange
            var rawOcrData = @"
                Form W-4 (2023)
                Employee's Withholding Certificate
                
                Step 1: Enter Personal Information
                First name: John
                Middle initial: D
                Last name: Smith
                Address: 123 Main Street
                City: New York, State: NY ZIP: 10001
                Social Security Number: 123-45-6789
                
                Filing Status:
                [X] Single or Married filing separately
                [ ] Married filing jointly
                [ ] Head of household
                
                Step 2: Multiple Jobs
                [ ] Complete this step if you hold more than one job
                
                Step 3: Claim Dependents
                Qualifying children under 17: 2
                Other dependents: 1
                Total claim amount: $4500
                
                Step 4: Other Adjustments
                (a) Other income: $5000
                (b) Deductions: $2000
                (c) Extra withholding: $100
                
                Step 5: Sign Here
                Employee's signature: John D Smith
                Date: 01/15/2023
            ";

            // Act
            var result = await _processor.ProcessAsync(rawOcrData);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ProcessedW4>(result);
            
            var processedW4 = (ProcessedW4)result;
            Assert.NotNull(processedW4.W4Data);
            
            var w4Data = processedW4.W4Data;
            Assert.Equal("John", w4Data.FirstName);
            Assert.Equal("D", w4Data.MiddleName);
            Assert.Equal("Smith", w4Data.LastName);
            Assert.Equal("123 Main Street", w4Data.StreetAddress);
            Assert.Equal("New York", w4Data.City);
            Assert.Equal("NY", w4Data.State);
            Assert.Equal("10001", w4Data.ZipCode);
            Assert.Equal("XXX-XX-6789", w4Data.SSN); // Should be masked
            Assert.Equal("Single or Married filing separately", w4Data.FilingStatus);
            Assert.True(w4Data.IsSingleOrMarriedFilingSeparately);
            Assert.Equal(2, w4Data.QualifyingChildren);
            Assert.Equal(1, w4Data.OtherDependents);
            Assert.Equal(5000m, w4Data.OtherIncome);
            Assert.Equal(2000m, w4Data.Deductions);
            Assert.Equal(100m, w4Data.ExtraWithholding);
            Assert.True(w4Data.SignatureDetected);
            Assert.Equal(new DateTime(2023, 1, 15), w4Data.DateSigned);
            Assert.False(w4Data.IsPreTwentyTwentyFormat);
        }

        [Fact]
        public async Task ProcessAsync_WithValidPre2020W4_ExtractsAllowances()
        {
            // Arrange
            var rawOcrData = @"
                Form W-4 (2019)
                Employee's Withholding Allowance Certificate
                
                Employee Information:
                Name: Jane Doe
                SSN: 987-65-4321
                Address: 456 Oak Avenue
                City: Los Angeles, CA 90001
                
                Marital Status:
                [ ] Single
                [X] Married
                [ ] Married, but withhold at higher Single rate
                
                Total number of allowances: 3
                Additional amount to withhold: $50
                
                [ ] I claim exempt from withholding
                
                Employee signature: Jane Doe
                Date: 12/01/2019
            ";

            // Act
            var result = await _processor.ProcessAsync(rawOcrData);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ProcessedW4>(result);
            
            var processedW4 = (ProcessedW4)result;
            var w4Data = processedW4.W4Data;
            
            Assert.Equal("Jane", w4Data.FirstName);
            Assert.Equal("Doe", w4Data.LastName);
            Assert.Equal("XXX-XX-4321", w4Data.SSN); // Should be masked
            Assert.Equal(3, w4Data.TotalAllowances);
            Assert.Equal(50m, w4Data.ExtraWithholding);
            Assert.False(w4Data.IsExempt);
            Assert.True(w4Data.IsPreTwentyTwentyFormat);
        }

        [Fact]
        public async Task ProcessAsync_WithEmptyInput_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _processor.ProcessAsync(""));
        }

        [Fact]
        public async Task ProcessAsync_WithNullInput_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _processor.ProcessAsync(null));
        }

        #endregion

        #region SSN Validation Tests

        [Theory]
        [InlineData("123-45-6789", "XXX-XX-6789", true, "Valid SSN should be masked")]
        [InlineData("000-12-3456", null, false, "SSN with 000 area should be invalid")]
        [InlineData("666-12-3456", null, false, "SSN with 666 area should be invalid")]
        [InlineData("900-12-3456", null, false, "SSN with 900+ area should be invalid")]
        [InlineData("123-00-4567", null, false, "SSN with 00 group should be invalid")]
        [InlineData("123-45-0000", null, false, "SSN with 0000 serial should be invalid")]
        public async Task ProcessAsync_WithVariousSSNFormats_HandlesCorrectly(
            string inputSSN, string expectedSSN, bool shouldBeValid, string scenario)
        {
            // Arrange
            var rawOcrData = $@"
                Form W-4 (2023)
                Name: Test User
                SSN: {inputSSN}
                Filing Status: Single
            ";

            // Act
            var result = await _processor.ProcessAsync(rawOcrData);
            var processedW4 = (ProcessedW4)result;
            var w4Data = processedW4.W4Data;

            // Assert
            if (shouldBeValid)
            {
                Assert.Equal(expectedSSN, w4Data.SSN);
                Assert.DoesNotContain("Invalid SSN", w4Data.ValidationErrors);
            }
            else
            {
                Assert.True(string.IsNullOrEmpty(w4Data.SSN) || 
                           w4Data.ValidationErrors.Any(e => e.Contains("SSN")));
            }
        }

        #endregion

        #region Filing Status Tests

        [Theory]
        [InlineData("[X] Single or Married filing separately", "Single or Married filing separately", true, false, false)]
        [InlineData("[X] Married filing jointly", "Married filing jointly", false, true, false)]
        [InlineData("[X] Head of household", "Head of household", false, false, true)]
        public async Task ProcessAsync_WithFilingStatus_ExtractsCorrectly(
            string filingStatusText, string expectedStatus, 
            bool expectSingle, bool expectJointly, bool expectHead)
        {
            // Arrange
            var rawOcrData = $@"
                Form W-4 (2023)
                Filing Status:
                {filingStatusText}
            ";

            // Act
            var result = await _processor.ProcessAsync(rawOcrData);
            var processedW4 = (ProcessedW4)result;
            var w4Data = processedW4.W4Data;

            // Assert
            Assert.Equal(expectedStatus, w4Data.FilingStatus);
            Assert.Equal(expectSingle, w4Data.IsSingleOrMarriedFilingSeparately);
            Assert.Equal(expectJointly, w4Data.IsMarriedFilingJointly);
            Assert.Equal(expectHead, w4Data.IsHeadOfHousehold);
        }

        #endregion

        #region Confidence Score Tests

        [Fact]
        public async Task ProcessAsync_WithCompleteForm_HasHighConfidenceScore()
        {
            // Arrange
            var rawOcrData = @"
                Form W-4 (2023)
                Name: John Smith
                SSN: 123-45-6789
                Address: 123 Main St
                City: New York, NY 10001
                Filing Status: [X] Single
                Employee signature: John Smith
                Date: 01/01/2023
            ";

            // Act
            var result = await _processor.ProcessAsync(rawOcrData);
            var processedW4 = (ProcessedW4)result;

            // Assert
            Assert.True(processedW4.ConfidenceScore > 0.7, 
                $"Expected confidence > 0.7, got {processedW4.ConfidenceScore}");
        }

        [Fact]
        public async Task ProcessAsync_WithMinimalForm_HasLowConfidenceScore()
        {
            // Arrange
            var rawOcrData = @"
                Form W-4
                Name: John
            ";

            // Act
            var result = await _processor.ProcessAsync(rawOcrData);
            var processedW4 = (ProcessedW4)result;

            // Assert
            Assert.True(processedW4.ConfidenceScore < 0.5, 
                $"Expected confidence < 0.5, got {processedW4.ConfidenceScore}");
        }

        #endregion

        #region Validation Tests

        [Fact]
        public async Task ProcessAsync_WithMissingRequiredFields_AddsValidationErrors()
        {
            // Arrange
            var rawOcrData = @"
                Form W-4 (2023)
                Name: John
                City: New York
            ";

            // Act
            var result = await _processor.ProcessAsync(rawOcrData);
            var processedW4 = (ProcessedW4)result;

            // Assert
            Assert.NotEmpty(processedW4.ValidationErrors);
            Assert.Contains(processedW4.ValidationErrors, e => e.Contains("Social Security Number"));
            Assert.Contains(processedW4.ValidationErrors, e => e.Contains("address"));
        }

        [Fact]
        public async Task ProcessAsync_WithFutureDate_AddsValidationError()
        {
            // Arrange
            var futureDate = DateTime.Now.AddDays(30).ToString("MM/dd/yyyy");
            var rawOcrData = $@"
                Form W-4 (2023)
                Name: John Smith
                SSN: 123-45-6789
                Date: {futureDate}
            ";

            // Act
            var result = await _processor.ProcessAsync(rawOcrData);
            var processedW4 = (ProcessedW4)result;

            // Assert
            Assert.Contains(processedW4.ValidationErrors, e => e.Contains("future"));
        }

        #endregion

        #region Multiple Jobs Tests

        [Fact]
        public async Task ProcessAsync_WithMultipleJobsChecked_ExtractsCorrectly()
        {
            // Arrange
            var rawOcrData = @"
                Form W-4 (2023)
                Step 2: Multiple Jobs
                [X] Complete this step if you hold more than one job
                (c) [X] If there are only two jobs total
            ";

            // Act
            var result = await _processor.ProcessAsync(rawOcrData);
            var processedW4 = (ProcessedW4)result;
            var w4Data = processedW4.W4Data;

            // Assert
            Assert.True(w4Data.HasMultipleJobs);
            Assert.True(w4Data.TwoJobsTotal);
        }

        #endregion

        #region Employer Information Tests

        [Fact]
        public async Task ProcessAsync_WithEmployerInfo_ExtractsCorrectly()
        {
            // Arrange
            var rawOcrData = @"
                Form W-4 (2023)
                Employer name: Acme Corporation
                Employer EIN: 12-3456789
                First date of employment: 01/01/2023
            ";

            // Act
            var result = await _processor.ProcessAsync(rawOcrData);
            var processedW4 = (ProcessedW4)result;
            var w4Data = processedW4.W4Data;

            // Assert
            Assert.Equal("Acme Corporation", w4Data.EmployerName);
            Assert.Equal("12-3456789", w4Data.EmployerEIN);
            Assert.Equal(new DateTime(2023, 1, 1), w4Data.FirstDateOfEmployment);
        }

        #endregion
    }
}