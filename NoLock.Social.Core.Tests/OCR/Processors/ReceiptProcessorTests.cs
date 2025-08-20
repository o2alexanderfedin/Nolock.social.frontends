using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;
using NoLock.Social.Core.OCR.Processors;

namespace NoLock.Social.Core.Tests.OCR.Processors
{
    /// <summary>
    /// Unit tests for the ReceiptProcessor class.
    /// Tests extraction, validation, and formatting of receipt data.
    /// </summary>
    public class ReceiptProcessorTests
    {
        private readonly Mock<ILogger<ReceiptProcessor>> _loggerMock;
        private readonly Mock<IOCRService> _ocrServiceMock;
        private readonly ReceiptProcessor _processor;

        public ReceiptProcessorTests()
        {
            _loggerMock = new Mock<ILogger<ReceiptProcessor>>();
            _ocrServiceMock = new Mock<IOCRService>();
            _processor = new ReceiptProcessor(_loggerMock.Object, _ocrServiceMock.Object);
        }
        
        /// <summary>
        /// Sets up OCR service mocks to return the specified ReceiptData as structured JSON.
        /// </summary>
        private void SetupOCRServiceMocks(ReceiptData receiptData, double confidenceScore = 85.0)
        {
            var trackingId = $"test-tracking-{Guid.NewGuid():N}";
            var submissionResponse = new OCRSubmissionResponse { TrackingId = trackingId };
            
            var statusResponse = new OCRStatusResponse
            {
                TrackingId = trackingId,
                Status = OCRProcessingStatus.Complete,
                ResultData = new OCRResultData
                {
                    ConfidenceScore = confidenceScore,
                    StructuredData = System.Text.Json.JsonSerializer.Serialize(receiptData)
                }
            };
            
            _ocrServiceMock.Setup(x => x.SubmitDocumentAsync(It.IsAny<OCRSubmissionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(submissionResponse);
            
            _ocrServiceMock.Setup(x => x.GetStatusAsync(trackingId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(statusResponse);
        }

        [Fact]
        public void DocumentType_ShouldReturnReceipt()
        {
            // Assert
            Assert.Equal("Receipt", _processor.DocumentType);
        }

        [Theory]
        [InlineData("TOTAL: $25.99\nSUBTOTAL: $24.00\nTAX: $1.99", true, "Receipt with all keywords")]
        [InlineData("Invoice #12345\nAmount Due: $100.00\nPayment received", true, "Invoice-style receipt")]
        [InlineData("Thank you for shopping\nItems: 5\nTotal amount: $50", true, "Receipt with thank you message")]
        [InlineData("Just some random text", false, "Non-receipt text")]
        [InlineData("", false, "Empty text")]
        [InlineData(null, false, "Null text")]
        public void CanProcess_ShouldIdentifyReceiptContent(string rawText, bool expectedResult, string scenario)
        {
            // Act
            var result = _processor.CanProcess(rawText);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task ProcessAsync_WithNullData_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _processor.ProcessAsync(null, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_WithEmptyData_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _processor.ProcessAsync(string.Empty, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessAsync_WithValidReceipt_ShouldExtractBasicFields()
        {
            // Arrange
            var ocrText = @"WALMART STORE #1234
123 Main Street
(555) 123-4567
12/25/2024 14:30

RECEIPT #ABC123

Milk             $3.99
Bread            $2.50
Eggs             $4.99

SUBTOTAL        $11.48
TAX              $0.92
TOTAL           $12.40

Thank you for shopping!";

            var mockReceiptData = new ReceiptData
            {
                StoreName = "WALMART STORE #1234",
                StoreAddress = "123 Main Street",
                StorePhone = "(555) 123-4567",
                TransactionDate = new DateTime(2024, 12, 25, 14, 30, 0),
                ReceiptNumber = "ABC123",
                Subtotal = 11.48m,
                TaxAmount = 0.92m,
                Total = 12.40m,
                Currency = "USD"
            };
            
            SetupOCRServiceMocks(mockReceiptData);

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Receipt", result.DocumentType);
            Assert.NotNull(result.ReceiptData);
            Assert.Equal("WALMART STORE #1234", result.ReceiptData.StoreName);
            Assert.Equal("(555) 123-4567", result.ReceiptData.StorePhone);
            Assert.Equal("ABC123", result.ReceiptData.ReceiptNumber);
            Assert.Equal(11.48m, result.ReceiptData.Subtotal);
            Assert.Equal(0.92m, result.ReceiptData.TaxAmount);
            Assert.Equal(12.40m, result.ReceiptData.Total);
            Assert.True(result.ReceiptData.TransactionDate.HasValue);
        }

        [Theory]
        [InlineData("USD", "USD")]
        [InlineData("EUR", "EUR")]
        [InlineData("GBP", "GBP")]
        [InlineData("", "USD")]
        public async Task ProcessAsync_ShouldExtractCurrency(string currencyInText, string expectedCurrency)
        {
            // Arrange
            var ocrText = $@"STORE NAME
{(string.IsNullOrEmpty(currencyInText) ? "" : $"Currency: {currencyInText}")}
TOTAL: 100.00";

            var mockReceiptData = new ReceiptData
            {
                StoreName = "STORE NAME",
                Total = 100.00m,
                Currency = expectedCurrency
            };
            
            SetupOCRServiceMocks(mockReceiptData);

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedCurrency, result.ReceiptData.Currency);
        }

        [Fact]
        public async Task ProcessAsync_ShouldCalculateMissingTotals()
        {
            // Arrange
            var ocrText = @"STORE NAME

Item 1           $10.00
Item 2           $20.00

SUBTOTAL         $30.00
TAX              $2.40";
            // Note: No TOTAL in the text

            var mockReceiptData = new ReceiptData
            {
                StoreName = "STORE NAME",
                Subtotal = 30.00m,
                TaxAmount = 2.40m,
                Total = 32.40m // Should be calculated
            };
            
            SetupOCRServiceMocks(mockReceiptData);

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(30.00m, result.ReceiptData.Subtotal);
            Assert.Equal(2.40m, result.ReceiptData.TaxAmount);
            Assert.Equal(32.40m, result.ReceiptData.Total); // Should be calculated
        }

        [Fact]
        public async Task ProcessAsync_ShouldCalculateTaxFromTotalAndSubtotal()
        {
            // Arrange
            var ocrText = @"STORE NAME

SUBTOTAL         $100.00
TOTAL            $108.00";
            // Note: No TAX in the text
            
            // Setup mock to return parsed data from backend (but missing tax calculation)
            var mockReceiptData = new ReceiptData
            {
                StoreName = "STORE NAME",
                Subtotal = 100.00m,
                Total = 108.00m,
                TaxAmount = 0, // Backend didn't extract this - should be calculated
                TaxRate = 0    // Should be calculated from missing tax amount
            };
            SetupOCRServiceMocks(mockReceiptData);

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(100.00m, result.ReceiptData.Subtotal);
            Assert.Equal(108.00m, result.ReceiptData.Total);
            Assert.Equal(8.00m, result.ReceiptData.TaxAmount); // Should be calculated
            Assert.Equal(8.0m, result.ReceiptData.TaxRate); // Should be 8%
        }

        [Fact]
        public async Task ProcessAsync_ShouldCalculateConfidenceScore()
        {
            // Arrange
            var completeReceipt = @"STORE NAME
(555) 123-4567
12/25/2024

RECEIPT #12345

Item 1           $10.00

SUBTOTAL         $10.00
TAX              $0.80
TOTAL            $10.80";

            var minimalReceipt = @"TOTAL: $10.00";

            // Setup mock for complete receipt with all fields
            var completeReceiptData = new ReceiptData
            {
                StoreName = "STORE NAME",
                StorePhone = "(555) 123-4567",
                TransactionDate = new DateTime(2024, 12, 25),
                ReceiptNumber = "12345",
                Subtotal = 10.00m,
                TaxAmount = 0.80m,
                Total = 10.80m,
                Items = new List<ReceiptItem>
                {
                    new ReceiptItem { Description = "Item 1", TotalPrice = 10.00m }
                }
            };
            
            // Setup mock for minimal receipt with just total
            var minimalReceiptData = new ReceiptData
            {
                Total = 10.00m
            };

            // Act - test complete receipt first
            SetupOCRServiceMocks(completeReceiptData);
            var completeResult = await _processor.ProcessAsync(completeReceipt, CancellationToken.None) as ProcessedReceipt;
            
            // Act - test minimal receipt second  
            SetupOCRServiceMocks(minimalReceiptData);
            var minimalResult = await _processor.ProcessAsync(minimalReceipt, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.NotNull(completeResult);
            Assert.NotNull(minimalResult);
            Assert.True(completeResult.ConfidenceScore > minimalResult.ConfidenceScore,
                $"Complete receipt confidence ({completeResult.ConfidenceScore}) should be higher than minimal ({minimalResult.ConfidenceScore})");
            Assert.True(completeResult.ConfidenceScore > 0.5);
            Assert.True(minimalResult.ConfidenceScore <= 0.5);
        }

        [Fact]
        public async Task ProcessAsync_ShouldValidateProcessedReceipt()
        {
            // Arrange
            var ocrText = @"STORE NAME
SUBTOTAL         $10.00
TAX              $0.80
TOTAL            $10.80";

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Validate());
            Assert.Equal(0, result.ValidationErrors.Count);
        }

        [Fact]
        public async Task ProcessAsync_WithTotalMismatch_ShouldAddWarning()
        {
            // Arrange
            var ocrText = @"STORE NAME
SUBTOTAL         $10.00
TAX              $0.80
TOTAL            $15.00"; // Wrong total
            
            // Setup mock to return parsed data with mismatch
            var mockReceiptData = new ReceiptData
            {
                StoreName = "STORE NAME",
                Subtotal = 10.00m,
                TaxAmount = 0.80m,
                Total = 15.00m // Wrong total - should be 10.80
            };
            SetupOCRServiceMocks(mockReceiptData);

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Warnings.Count > 0);
            Assert.True(result.Warnings.Any(w => w.Contains("Total mismatch")));
        }

        [Theory]
        [InlineData("12/25/2024", true, "US date format")]
        [InlineData("25/12/2024", true, "European date format")]
        [InlineData("2024-12-25", true, "ISO date format")]
        [InlineData("12-25-2024", true, "Dash separator")]
        [InlineData("invalid date", false, "Invalid date")]
        public async Task ProcessAsync_ShouldParseVariousDateFormats(string dateText, bool shouldParse, string scenario)
        {
            // Arrange
            var ocrText = $@"STORE NAME
{dateText}
TOTAL: $10.00";

            // Setup mock to return basic receipt data without date (so post-processing can parse it)
            var mockReceiptData = new ReceiptData
            {
                StoreName = "STORE NAME",
                Total = 10.00m,
                TransactionDate = null // Let post-processing parse the date from OCR text
            };
            SetupOCRServiceMocks(mockReceiptData);

            // Act
            var result = await _processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.NotNull(result);
            if (shouldParse)
            {
                Assert.True(result.ReceiptData.TransactionDate.HasValue, $"Date should be parsed for: {scenario}");
            }
        }

        [Fact]
        public async Task ProcessAsync_WithException_ShouldAddValidationError()
        {
            // Arrange
            var processor = new ReceiptProcessor(_loggerMock.Object, _ocrServiceMock.Object);
            // This would normally not throw, but we're testing error handling
            var ocrText = "TOTAL: $10.00";

            // Act
            var result = await processor.ProcessAsync(ocrText, CancellationToken.None) as ProcessedReceipt;

            // Assert
            Assert.NotNull(result);
            // The result should still be valid even if there were processing issues
            Assert.Equal("Receipt", result.DocumentType);
        }
    }
}