using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NoLock.Social.Core.OCR.Models;
using CameraDocumentType = NoLock.Social.Core.Camera.Models.DocumentType;
using NoLock.Social.Core.OCR.Processors;
using Xunit;

namespace NoLock.Social.Core.Tests.OCR.Processors
{
    /// <summary>
    /// Unit tests for the CheckProcessor class.
    /// </summary>
    public class CheckProcessorTests
    {
        private readonly Mock<ILogger<CheckProcessor>> _loggerMock;
        private readonly CheckProcessor _processor;

        public CheckProcessorTests()
        {
            _loggerMock = new Mock<ILogger<CheckProcessor>>();
            _processor = new CheckProcessor(_loggerMock.Object);
        }

        [Fact]
        public void DocumentType_ShouldReturnCheck()
        {
            // Assert
            Assert.Equal(CameraDocumentType.Check.ToString(), _processor.DocumentType);
        }

        [Theory]
        [InlineData("PAY TO THE ORDER OF John Doe\n$1,234.56\nMEMO: Rent\nROUTING: 123456789", true, "Contains check keywords")]
        [InlineData("⑆123456789⑆1234567890⑆1001", true, "Contains MICR line")]
        [InlineData(":123456789:1234567890:1001", true, "Contains alternative MICR pattern")]
        [InlineData("Random text without any check information", false, "No check indicators")]
        [InlineData("", false, "Empty string")]
        [InlineData(null, false, "Null string")]
        [InlineData("bank account signature memo dollars", true, "Multiple check keywords")]
        public void CanProcess_ShouldIdentifyCheckDocuments(string input, bool expected, string scenario)
        {
            // Act
            var result = _processor.CanProcess(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task ProcessAsync_WithNullInput_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _processor.ProcessAsync(null, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_WithEmptyInput_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _processor.ProcessAsync("", CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_WithValidCheckData_ShouldExtractAllFields()
        {
            // Arrange
            var ocrData = @"
                FIRST NATIONAL BANK
                John Smith
                123 Main Street
                Anytown, NY 12345
                
                CHECK #1001                           Date: 12/15/2023
                
                PAY TO THE ORDER OF: ABC Company Inc.
                
                $1,234.56
                One thousand two hundred thirty-four and 56/100 DOLLARS
                
                MEMO: Invoice #12345
                
                ⑆123456789⑆1234567890⑆1001
            ";

            // Act
            var result = await _processor.ProcessAsync(ocrData, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            var processedCheck = Assert.IsType<ProcessedCheck>(result);
            Assert.Equal(CameraDocumentType.Check.ToString(), processedCheck.DocumentType);
            Assert.NotNull(processedCheck.CheckData);
            
            var checkData = processedCheck.CheckData;
            Assert.Equal("123456789", checkData.RoutingNumber);
            Assert.Equal("1234567890", checkData.AccountNumber);
            Assert.Equal("1001", checkData.CheckNumber);
            Assert.Equal("ABC Company Inc.", checkData.Payee);
            Assert.Equal(1234.56m, checkData.AmountNumeric);
            Assert.Equal("Invoice #12345", checkData.Memo);
            Assert.Equal(new DateTime(2023, 12, 15), checkData.Date);
        }

        [Theory]
        [InlineData("121000248", true, "Valid Wells Fargo routing number")]
        [InlineData("026009593", true, "Valid Bank of America routing number")]
        [InlineData("123456789", false, "Invalid checksum")]
        [InlineData("12345678", false, "Too short")]
        [InlineData("1234567890", false, "Too long")]
        [InlineData("12345678a", false, "Contains letter")]
        [InlineData("", false, "Empty string")]
        [InlineData(null, false, "Null string")]
        public async Task ProcessAsync_ShouldValidateRoutingNumbers(string routingNumber, bool expectedValid, string scenario)
        {
            // Arrange
            var ocrData = $@"
                PAY TO: Test Payee
                AMOUNT: $100.00
                {routingNumber ?? ""}⑆1234567890⑆1001
            ";

            // Act
            var result = await _processor.ProcessAsync(ocrData, CancellationToken.None);

            // Assert
            var processedCheck = Assert.IsType<ProcessedCheck>(result);
            Assert.Equal(expectedValid, processedCheck.CheckData.IsRoutingNumberValid);
        }

        [Theory]
        [InlineData("$1,234.56", "one thousand two hundred thirty-four and 56/100", true, "Matching amounts")]
        [InlineData("$500.00", "five hundred and 00/100", true, "Round amount match")]
        [InlineData("$1,234.56", "two thousand and 00/100", false, "Mismatched amounts")]
        [InlineData("$100.50", "one hundred and 49/100", false, "Cent mismatch")]
        public async Task ProcessAsync_ShouldVerifyAmountConsistency(
            string numericAmount, string writtenAmount, bool expectedMatch, string scenario)
        {
            // Arrange
            var ocrData = $@"
                PAY TO: Test Payee
                {numericAmount}
                {writtenAmount} DOLLARS
                123456789⑆1234567890⑆1001
            ";

            // Act
            var result = await _processor.ProcessAsync(ocrData, CancellationToken.None);

            // Assert
            var processedCheck = Assert.IsType<ProcessedCheck>(result);
            Assert.Equal(expectedMatch, processedCheck.CheckData.AmountsMatch);
        }

        [Theory]
        [InlineData("twenty-five", 25.00, "Hyphenated number")]
        [InlineData("one hundred", 100.00, "Round hundred")]
        [InlineData("three thousand five hundred", 3500.00, "Thousands")]
        [InlineData("forty-two and 99/100", 42.99, "With cents")]
        [InlineData("two million one hundred thousand", 2100000.00, "Millions")]
        [InlineData("seventeen", 17.00, "Teen number")]
        [InlineData("ninety-nine and 01/100", 99.01, "High tens with cents")]
        public async Task ProcessAsync_ShouldParseWrittenAmounts(string writtenAmount, decimal expectedAmount, string scenario)
        {
            // Arrange
            var ocrData = $@"
                PAY TO: Test Payee
                ${expectedAmount:F2}
                {writtenAmount} DOLLARS
                026009593⑆1234567890⑆1001
            ";

            // Act
            var result = await _processor.ProcessAsync(ocrData, CancellationToken.None);

            // Assert
            var processedCheck = Assert.IsType<ProcessedCheck>(result);
            Assert.NotNull(processedCheck.CheckData.AmountWrittenParsed);
            Assert.Equal(expectedAmount, processedCheck.CheckData.AmountWrittenParsed.Value, 2);
        }

        [Fact]
        public async Task ProcessAsync_ShouldExtractMicrLineData()
        {
            // Arrange
            var ocrData = @"
                Check content here
                ⑆026009593⑆9876543210⑆2001
            ";

            // Act
            var result = await _processor.ProcessAsync(ocrData, CancellationToken.None);

            // Assert
            var processedCheck = Assert.IsType<ProcessedCheck>(result);
            var checkData = processedCheck.CheckData;
            Assert.Equal("026009593", checkData.RoutingNumber);
            Assert.Equal("9876543210", checkData.AccountNumber);
            Assert.Equal("2001", checkData.CheckNumber);
            Assert.True(checkData.IsRoutingNumberValid);
        }

        [Fact]
        public async Task ProcessAsync_WithAlternativeMicrPattern_ShouldExtractData()
        {
            // Arrange
            var ocrData = @"
                Check content here
                :026009593:9876543210:2001
            ";

            // Act
            var result = await _processor.ProcessAsync(ocrData, CancellationToken.None);

            // Assert
            var processedCheck = Assert.IsType<ProcessedCheck>(result);
            var checkData = processedCheck.CheckData;
            Assert.Equal("026009593", checkData.RoutingNumber);
            Assert.Equal("9876543210", checkData.AccountNumber);
            Assert.Equal("2001", checkData.CheckNumber);
        }

        [Fact]
        public async Task ProcessAsync_ShouldCalculateConfidenceScore()
        {
            // Arrange
            var completeCheckData = @"
                BANK OF AMERICA
                PAY TO THE ORDER OF: John Doe
                $500.00
                Five hundred and 00/100 DOLLARS
                Date: 01/15/2024
                MEMO: Payment
                ⑆026009593⑆1234567890⑆1001
            ";

            var incompleteCheckData = @"
                PAY TO: Someone
                Amount: $100
            ";

            // Act
            var completeResult = await _processor.ProcessAsync(completeCheckData, CancellationToken.None);
            var incompleteResult = await _processor.ProcessAsync(incompleteCheckData, CancellationToken.None);

            // Assert
            var completeCheck = Assert.IsType<ProcessedCheck>(completeResult);
            var incompleteCheck = Assert.IsType<ProcessedCheck>(incompleteResult);
            
            Assert.True(completeCheck.ConfidenceScore > incompleteCheck.ConfidenceScore,
                $"Complete check confidence ({completeCheck.ConfidenceScore}) should be higher than incomplete ({incompleteCheck.ConfidenceScore})");
            Assert.True(completeCheck.ConfidenceScore > 0.7, "Complete check should have high confidence");
            Assert.True(incompleteCheck.ConfidenceScore < 0.5, "Incomplete check should have low confidence");
        }

        [Fact]
        public async Task ProcessAsync_ShouldDetectSignatureIndicators()
        {
            // Arrange
            var checkWithSignature = @"
                PAY TO: Test
                $100.00
                AUTHORIZED SIGNATURE: _____________
                026009593⑆1234567890⑆1001
            ";

            var checkWithoutSignature = @"
                PAY TO: Test
                $100.00
                026009593⑆1234567890⑆1001
            ";

            // Act
            var withSigResult = await _processor.ProcessAsync(checkWithSignature, CancellationToken.None);
            var withoutSigResult = await _processor.ProcessAsync(checkWithoutSignature, CancellationToken.None);

            // Assert
            var checkWithSig = Assert.IsType<ProcessedCheck>(withSigResult);
            var checkWithoutSig = Assert.IsType<ProcessedCheck>(withoutSigResult);
            
            Assert.True(checkWithSig.CheckData.SignatureDetected);
            Assert.True(checkWithSig.CheckData.SignatureConfidence > 0);
            Assert.False(checkWithoutSig.CheckData.SignatureDetected);
            Assert.Equal(0, checkWithoutSig.CheckData.SignatureConfidence);
        }

        [Fact]
        public async Task ProcessAsync_WithInvalidData_ShouldAddValidationErrors()
        {
            // Arrange
            var invalidCheckData = @"
                Some text that looks like a check
                but missing critical information
            ";

            // Act
            var result = await _processor.ProcessAsync(invalidCheckData, CancellationToken.None);

            // Assert
            var processedCheck = Assert.IsType<ProcessedCheck>(result);
            Assert.NotEmpty(processedCheck.CheckData.ValidationErrors);
            Assert.Contains(processedCheck.CheckData.ValidationErrors, 
                e => e.Contains("Routing number") || e.Contains("routing"));
        }

        [Fact]
        public void CheckData_Validate_ShouldValidateRequiredFields()
        {
            // Arrange
            var checkData = new CheckData();

            // Act
            var isValid = checkData.Validate();

            // Assert
            Assert.False(isValid);
            Assert.NotEmpty(checkData.ValidationErrors);
            Assert.Contains(checkData.ValidationErrors, e => e.Contains("Routing number"));
            Assert.Contains(checkData.ValidationErrors, e => e.Contains("Account number"));
            Assert.Contains(checkData.ValidationErrors, e => e.Contains("Check number"));
            Assert.Contains(checkData.ValidationErrors, e => e.Contains("amount"));
            Assert.Contains(checkData.ValidationErrors, e => e.Contains("Payee"));
            Assert.Contains(checkData.ValidationErrors, e => e.Contains("Date"));
        }

        [Fact]
        public void CheckData_Validate_WithCompleteData_ShouldReturnTrue()
        {
            // Arrange
            var checkData = new CheckData
            {
                RoutingNumber = "026009593",
                AccountNumber = "1234567890",
                CheckNumber = "1001",
                AmountNumeric = 100.00m,
                AmountWrittenParsed = 100.00m,
                Payee = "John Doe",
                Date = DateTime.Now,
                IsRoutingNumberValid = true
            };

            // Act
            var isValid = checkData.Validate();

            // Assert
            Assert.True(isValid);
            Assert.Empty(checkData.ValidationErrors);
            Assert.True(checkData.AmountsMatch);
        }
    }
}